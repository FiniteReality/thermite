using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Thermite.Codecs;
using Thermite.Natives;

using static Thermite.Internal.FrameParsingUtilities;
using static Thermite.Natives.Opus;
using static Thermite.Internal.ThrowHelpers;

namespace Thermite.Transcoders.Opus
{
    internal sealed class OpusDecodingTranscoder : IAudioTranscoder
    {
        private const int OutputBufferSize = 1 << 12; // 4k

        private readonly OpusAudioCodec _codec;
        private readonly PipeReader _input;
        private readonly Pipe _pipe;

        private unsafe OpusDecoder* _decoder;

        public PipeReader Output => _pipe.Reader;

        internal unsafe OpusDecodingTranscoder(PipeReader input,
            OpusAudioCodec codec)
        {
            _codec = codec;

            int status;
            _decoder = opus_decoder_create(codec.SamplingRate,
                codec.ChannelCount, &status);

            if (status < 0)
                ThrowExternalException("Could not create Opus Decoder",
                    status);

            _input = input;
            _pipe = new Pipe();
        }

        ~OpusDecodingTranscoder()
        {
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

                        var encoded = TryDecodeFrame(frame, writer);
                        Debug.Assert(encoded > 0, "Opus decode error");

                        if (encoded > 0)
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
                new PcmAudioCodec(
                    bitDepth: sizeof(short) * 8,
                    _codec.ChannelCount,
                    SampleEndianness.LittleEndian,
                    SampleFormat.SignedInteger,
                    _codec.SamplingRate));

        private unsafe int TryDecodeFrame(ReadOnlySequence<byte> frame,
            PipeWriter writer)
        {
            if (frame.IsSingleSegment)
                return WriteInternal(_decoder, _codec.ChannelCount,
                    frame.FirstSpan, writer);

            var buffer = ArrayPool<byte>.Shared.Rent((int)frame.Length);
            frame.CopyTo(buffer);

            var status = WriteInternal(_decoder, _codec.ChannelCount,
                buffer.AsSpan().Slice(0, (int)frame.Length),
                writer);

            ArrayPool<byte>.Shared.Return(buffer);

            return status;

            static unsafe int WriteInternal(OpusDecoder* decoder,
                int channelCount, ReadOnlySpan<byte> frame, PipeWriter writer)
            {
                var block = writer.GetSpan(OutputBufferSize);
                int blockSize = block.Length / sizeof(short) / channelCount;

                int encoded;
                fixed (byte* opusData = frame)
                fixed (short* outputBlock = MemoryMarshal
                    .Cast<byte, short>(block))
                    encoded = opus_decode(decoder, opusData, frame.Length,
                        outputBlock + 1, blockSize, 1);

                if (encoded > 0 &&
                    !BinaryPrimitives.TryWriteInt16LittleEndian(block,
                        (short)encoded))
                    return -1;

                return encoded;
            }
        }

        public unsafe ValueTask DisposeAsync()
        {
            if (_decoder != null)
                opus_decoder_destroy(_decoder);

            _decoder = null;
            return default;
        }
    }
}
