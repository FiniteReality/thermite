using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Thermite.Codecs;

namespace Thermite.Decoders.Mpeg
{
    /// <summary>
    /// A decoder for decoding MPEG 1, 2 and 3 audio files.
    /// </summary>
    public sealed class MpegAudioDecoder : IAudioDecoder
    {
        private readonly PipeReader _input;
        private readonly Pipe _outputPipe;

        private TaskCompletionSource<MpegAudioCodec> _codec = null!;

        /// <inheritdoc/>
        public PipeReader Output => _outputPipe.Reader;

        internal MpegAudioDecoder(PipeReader input)
        {
            _input = input;
            _outputPipe = new Pipe();
        }

        /// <inheritdoc/>
        public async Task RunAsync(
            CancellationToken cancellationToken = default)
        {
            bool identified = false;
            _codec = new TaskCompletionSource<MpegAudioCodec>();

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

                    while (TryReadMpegFrame(ref buffer, out var frame,
                        out var version, out var layer, out var bitrate,
                        out var samplingRate, out var channelCount))
                    {
                        if (!identified)
                        {
                            identified = true;
                            _ = _codec.TrySetResult(
                                new MpegAudioCodec(version, layer,
                                    samplingRate, channelCount, bitrate));
                        }

                        if (!TryWriteFrame(frame, _outputPipe.Writer))
                            return;
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

            static bool TryWriteFrame(ReadOnlySequence<byte> frame,
                PipeWriter writer)
            {
                var span = writer.GetSpan((int)frame.Length + sizeof(short));

                if (!BinaryPrimitives.TryWriteInt16LittleEndian(
                    span, (short)frame.Length))
                {
                    writer.Advance(0);
                    return false;
                }

                frame.CopyTo(span.Slice(sizeof(short)));
                writer.Advance((int)frame.Length + sizeof(short));
                return true;
            }
        }

        private static bool TryReadMpegFrame(
            ref ReadOnlySequence<byte> buffer,
            out ReadOnlySequence<byte> frame,
            out int versionId, out int layer, out int bitrate,
            out int samplingRate, out int channelCount)
        {
            if (!MpegParser.TryReadMpegFrame(buffer, out frame, out versionId,
                out layer, out bitrate, out samplingRate, out channelCount))
                return false;

            buffer = buffer.Slice(frame.End);
            return true;
        }

        /// <inheritdoc/>
        public async ValueTask<IAudioCodec?> IdentifyCodecAsync(
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.CanBeCanceled)
                using (cancellationToken.Register(CancelPromise, _codec))
                    return await _codec.Task;

            return await _codec.Task;

            static void CancelPromise(object? state)
            {
                var codec = (TaskCompletionSource<MpegAudioCodec>)state!;

                _ = codec.TrySetCanceled();
            }
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            return default;
        }
    }
}
