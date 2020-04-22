using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Thermite.Codecs;

namespace Thermite.Decoders.Matroska
{
    /// <summary>
    /// A decoder for decoding Matroska and WEBM audio files.
    /// </summary>
    public sealed class MatroskaAudioDecoder : IAudioDecoder
    {
        // TODO: this assumes blocks are ordered linearly. While that is likely
        // the case, unordered blocks will cause issues.

        private readonly PipeReader _input;
        private readonly Pipe _outputPipe;

        // Mutable structs. Do not make readonly.
        private MatroskaState _state;
        private MatroskaTrack _currentAudioTrack;
        private MatroskaTrack _bestAudioTrack;

        /// <inheritdoc/>
        public PipeReader Output => _outputPipe.Reader;

        internal MatroskaAudioDecoder(PipeReader input)
        {
            _input = input;
            _outputPipe = new Pipe();
        }

        /// <inheritdoc/>
        public async Task RunAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                FlushResult flushResult = default;
                ReadResult readResult = default;
                while (!flushResult.IsCompleted)
                {
                    readResult = await _input.ReadAsync(cancellationToken);
                    var buffer = readResult.Buffer;

                    if (buffer.IsEmpty && readResult.IsCompleted)
                        return;

                    while (TryHandleElement(ref buffer, out var status))
                    {
                        if (status == EbmlHandleStatus.NewBlock &&
                            !TryWriteBlock(_bestAudioTrack, _outputPipe.Writer,
                                _state.BlockData))
                            return;

                        if (status == EbmlHandleStatus.UnsupportedFile ||
                            status == EbmlHandleStatus.NoMoreData)
                            return;

                        if (status == EbmlHandleStatus.MissingData)
                            break;
                    }

                    flushResult = await _outputPipe.Writer.FlushAsync(
                            cancellationToken);
                    _input.AdvanceTo(buffer.Start, buffer.End);
                }
            }
            finally
            {
                _ = await _outputPipe.Writer.FlushAsync(cancellationToken);
                await _input.CompleteAsync();
                await _outputPipe.Writer.CompleteAsync();
            }

            static bool TryWriteBlock(MatroskaTrack track, PipeWriter writer,
                ReadOnlySequence<byte> block)
            {
                if (block.IsSingleSegment)
                    return FastPath(track, writer, block.FirstSpan);

                var buffer = ArrayPool<byte>.Shared.Rent((int)block.Length);
                block.CopyTo(buffer);

                var slicedBuffer = buffer.AsSpan().Slice(0, (int)block.Length);
                var status = FastPath(track, writer, slicedBuffer);
                ArrayPool<byte>.Shared.Return(buffer);

                return status;

                static bool FastPath(MatroskaTrack track, PipeWriter writer,
                    ReadOnlySpan<byte> block)
                {
                    if (!EbmlParser.TryReadEbmlEncodedInt(ref block,
                        out var trackNumber) ||
                        trackNumber != track.TrackNumber)
                        return false;

                    if (!BinaryPrimitives.TryReadInt16BigEndian(block,
                        out var timecode))
                        return false;

                    block = block.Slice(sizeof(short));

                    var flags = block[0];
                    block = block.Slice(1);

                    var destination = writer.GetSpan(
                        block.Length + sizeof(short));

                    if (!BinaryPrimitives.TryWriteInt16LittleEndian(
                        destination, (short)block.Length))
                    {
                        writer.Advance(0);
                        return false;
                    }

                    if (!block.TryCopyTo(destination.Slice(sizeof(short))))
                    {
                        writer.Advance(0);
                        return false;
                    }

                    writer.Advance(block.Length + sizeof(short));
                    return true;
                }
            }
        }

        /// <inheritdoc/>
        public ValueTask<IAudioCodec?> IdentifyCodecAsync(
            CancellationToken cancellationToken = default)
        {
            return new ValueTask<IAudioCodec?>(_bestAudioTrack.CodecId switch
            {
                MatroskaCodec.Opus =>
                    new OpusAudioCodec((int)_bestAudioTrack.SampleRate,
                        (int)_bestAudioTrack.ChannelCount,
                        (int)_bestAudioTrack.BitDepth),
                MatroskaCodec.MpegLayer1 => CreateMpegDecoder(),
                MatroskaCodec.MpegLayer2 => CreateMpegDecoder(),
                MatroskaCodec.MpegLayer3 => CreateMpegDecoder(),
                _ => null,
            });

            static IAudioCodec? CreateMpegDecoder()
            {
                Debug.Assert(false,
                    "MPEG parameters are not passed through yet");
                return null;
            }
        }

        private bool TryHandleElement(ref ReadOnlySequence<byte> buffer,
            out EbmlHandleStatus status)
        {
            status = EbmlHandleStatus.MissingData;

            if (!EbmlParser.TryReadEbmlElement(buffer, out var elementId,
                    out var length, out var data))
                return false;

            var ebmlElement = (EbmlElementId)elementId;

            status = EbmlParser.TryHandleEbmlElement(ref _state,
                ref _currentAudioTrack, ebmlElement, length, ref data);

            if (status == EbmlHandleStatus.MissingData)
                return false;

            if (status == EbmlHandleStatus.NewTrack)
            {
                if (IsBetterTrack(_bestAudioTrack,
                    _currentAudioTrack))
                {
                    _bestAudioTrack.CodecData?.Dispose();
                    _bestAudioTrack = _currentAudioTrack;
                }
                else
                {
                    _currentAudioTrack.CodecData?.Dispose();
                }

                _currentAudioTrack = default;
            }

            buffer = data;
            return true;

            static bool IsBetterTrack(MatroskaTrack currentBest,
                MatroskaTrack other)
            {
                // TODO: add more criteria for this

                // automatically discard non-audio tracks
                if (!other.IsAudio)
                    return false;

                if (other.IsDefault && !currentBest.IsDefault)
                    return true;

                // if current best is not an audio track, we are
                // automatically better
                return !currentBest.IsAudio;
            }
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            _currentAudioTrack.CodecData?.Dispose();
            _bestAudioTrack.CodecData?.Dispose();
            return default;
        }
    }
}
