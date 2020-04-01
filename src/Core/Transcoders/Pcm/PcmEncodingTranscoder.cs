using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Thermite.Codecs;
using Thermite.Natives;

using static Thermite.Natives.Opus;
using static Thermite.Utilities.ThrowHelpers;

namespace Thermite.Transcoders.Pcm
{
    /// <summary>
    /// A transcoder which encodes PCM sample data to Opus as it passes
    /// through.
    /// </summary>
    public sealed class PcmEncodingTranscoder : IAudioTranscoder
    {
        private readonly PcmAudioCodec _codec;
        private readonly PipeReader _input;
        private readonly Pipe _pipe;

        private unsafe OpusEncoder* _encoder;

        /// <inheritdoc/>
        public PipeReader Output => _pipe.Reader;

        internal unsafe PcmEncodingTranscoder(PipeReader input,
            PcmAudioCodec codec)
        {
            const int Application = 2049; // OPUS_APPLICATION_AUDIO

            Debug.Assert(codec.BitDepth == sizeof(short) * 8);
            Debug.Assert(codec.ChannelCount == 2);
            Debug.Assert(codec.Endianness == SampleEndianness.LittleEndian);
            Debug.Assert(codec.Format == SampleFormat.SignedInteger);
            Debug.Assert(codec.SamplingRate == 48000);

            _codec = codec;
            int status;
            _encoder = opus_encoder_create(codec.SamplingRate,
                codec.ChannelCount, Application, &status);

            if (status < 0)
                ThrowExternalException("Could not create Opus Encoder",
                    status);

            _input = input;
            _pipe = new Pipe();
        }

        /// <summary>
        /// Finalizes an instance of <see cref="PcmEncodingTranscoder"/>.
        /// </summary>
        ~PcmEncodingTranscoder()
        {
            // N.B. not awaited since implementation of DisposeAsync is
            // synchronous.
            _ = DisposeAsync();
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        public async Task RunAsync(
            CancellationToken cancellationToken = default)
        {
            var writer = _pipe.Writer;
            try
            {
                ReadResult readResult = default;
                FlushResult flushResult = default;

                while (!readResult.IsCompleted && !flushResult.IsCompleted)
                {
                    readResult = await _input.ReadAsync(cancellationToken);
                    var sequence = readResult.Buffer;

                    while (TryReadFrame(ref sequence, out var frame))
                    {
                        if (frame.IsEmpty)
                            continue;

                        var encoded = TryEncodeFrame(frame, writer);
                        Debug.Assert(encoded > 0, "Opus encode error");

                        writer.Advance(encoded);
                    }

                    _input.AdvanceTo(sequence.Start, sequence.End);
                    flushResult = await writer.FlushAsync(cancellationToken);
                }
            }
            finally
            {
                await writer.CompleteAsync();
                await _input.CompleteAsync();
            }

            static bool TryReadFrame(
                ref ReadOnlySequence<byte> sequence,
                out ReadOnlySequence<byte> frame)
            {
                frame = default;
                var reader = new SequenceReader<byte>(sequence);

                if (!reader.TryReadLittleEndian(out short frameLength))
                    return false;

                if (sequence.Length < frameLength)
                    return false;

                frame = sequence.Slice(reader.Position, frameLength);
                var nextFrame = sequence.GetPosition(frameLength,
                    reader.Position);
                sequence = sequence.Slice(nextFrame);
                return true;
            }
        }

        /// <inheritdoc/>
        public ValueTask<IAudioCodec> GetOutputCodecAsync(
            CancellationToken cancellationToken = default)
            => new ValueTask<IAudioCodec>(
                OpusAudioCodec.DiscordCompatibleOpus);

        private unsafe int TryEncodeFrame(ReadOnlySequence<byte> frame,
            PipeWriter writer)
        {
            if (frame.IsSingleSegment)
                return WriteInternal(_encoder, _codec.ChannelCount,
                    frame.FirstSpan, writer);

            var buffer = ArrayPool<byte>.Shared.Rent((int)frame.Length);
            frame.CopyTo(buffer);

            var status = WriteInternal(_encoder, _codec.ChannelCount,
                buffer.AsSpan().Slice(0, (int)frame.Length),
                writer);

            ArrayPool<byte>.Shared.Return(buffer);

            return status;

            static unsafe int WriteInternal(OpusEncoder* encoder,
                int channelCount, ReadOnlySpan<byte> frame, PipeWriter writer)
            {
                var block = writer.GetSpan();

                int frameSize = frame.Length / sizeof(short) / channelCount;

                int encoded;
                fixed (byte* sampleData = frame)
                fixed (byte* outputBlock = block.Slice(2))
                    encoded = opus_encode(encoder, (short*)sampleData,
                        frameSize, outputBlock, block.Length);

                if (encoded > 0 &&
                    !BinaryPrimitives.TryWriteInt16LittleEndian(block,
                        (short)encoded))
                    return -1;

                return encoded;
            }
        }

        /// <inheritdoc/>
        public unsafe ValueTask DisposeAsync()
        {
            // N.B. Change implementation of finalizer if this becomes async!

            if (_encoder != null)
                opus_encoder_destroy(_encoder);

            _encoder = null;
            return default;
        }
    }
}
