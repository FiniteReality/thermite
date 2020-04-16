using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Thermite.Codecs;
using Thermite.Interop;

using static Thermite.Interop.Opus;
using static Thermite.Utilities.FrameParsingUtilities;
using static Thermite.Utilities.ThrowHelpers;

namespace Thermite.Transcoders.Pcm
{
    internal sealed class PcmEncodingTranscoder<T> : IAudioTranscoder
        where T : unmanaged
    {
        private readonly PcmAudioCodec _codec;
        private readonly PipeReader _input;
        private readonly Pipe _pipe;

        private unsafe OpusEncoder* _encoder;

        public PipeReader Output => _pipe.Reader;

        internal unsafe PcmEncodingTranscoder(PipeReader input,
            PcmAudioCodec codec)
        {
            const int Application = 2049; // OPUS_APPLICATION_AUDIO

            Debug.Assert(
                typeof(T) == typeof(short) ||
                typeof(T) == typeof(float));

            Debug.Assert(codec.ChannelCount == 2);
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

        ~PcmEncodingTranscoder()
        {
            // N.B. not awaited since implementation of DisposeAsync is
            // synchronous.
            _ = DisposeAsync();
            GC.SuppressFinalize(this);
        }

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
        }

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

                int frameSize = frame.Length / sizeof(T) / channelCount;

                int encoded = default;
                fixed (byte* sampleData = frame)
                fixed (byte* outputBlock = block.Slice(2))
                    if (typeof(T) == typeof(short))
                        encoded = opus_encode(encoder, (short*)sampleData,
                            frameSize, outputBlock, block.Length);
                    else if (typeof(T) == typeof(float))
                        encoded = opus_encode_float(encoder,
                            (float*)sampleData, frameSize, outputBlock,
                                block.Length);

                if (encoded > 0 &&
                    !BinaryPrimitives.TryWriteInt16LittleEndian(block,
                        (short)encoded))
                    return -1;

                return encoded;
            }
        }

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
