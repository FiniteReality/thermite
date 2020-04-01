using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Thermite.Codecs;
using Thermite.Natives;

using static Thermite.Natives.Opus;
using static Thermite.Utilities.ThrowHelpers;

namespace Thermite.Transcoders.Opus
{
    /// <summary>
    /// A transcoder which decodes Opus audio packets into PCM samples.
    /// </summary>
    public sealed class OpusAudioDecodingTranscoder : IAudioTranscoder
    {
        private readonly PipeReader _input;
        private readonly Pipe _pipe;

        private readonly int _sampleRate;
        private readonly int _channelCount;

        private unsafe OpusDecoder* _decoder;

        /// <inheritdoc/>
        public PipeReader Output => _pipe.Reader;

        internal unsafe OpusAudioDecodingTranscoder(PipeReader input,
            OpusAudioCodec inputCodec)
        {
            _sampleRate = inputCodec.SamplingRate;
            _channelCount = inputCodec.ChannelCount;

            int status;
            _decoder = opus_decoder_create(_sampleRate, _channelCount, &status);

            if (status < 0)
                ThrowExternalException("Could not create Opus Decoder",
                    status);

            _input = input;
            _pipe = new Pipe();
        }

        /// <summary>
        /// Finalizes an instance of
        /// <see cref="OpusAudioDecodingTranscoder"/>.
        /// </summary>
        ~OpusAudioDecodingTranscoder()
        {
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

                        if (!TryTranscodePacket(frame, writer))
                            break;
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

                if (!reader.TryReadLittleEndian(out short packetLength))
                    return false;

                if (sequence.Length < packetLength)
                    return false;

                frame = sequence.Slice(reader.Position, packetLength);
                var nextFrame = sequence.GetPosition(packetLength,
                    reader.Position);
                sequence = sequence.Slice(nextFrame);
                return true;
            }
        }

        /// <inheritdoc/>
        public ValueTask<IAudioCodec> GetOutputCodecAsync(
            CancellationToken cancellationToken = default)
            => new ValueTask<IAudioCodec>(
                new PcmAudioCodec(16, _channelCount,
                    SampleEndianness.LittleEndian, SampleFormat.SignedInteger,
                    _sampleRate));
        private unsafe bool TryTranscodePacket(ReadOnlySequence<byte> packet,
            PipeWriter writer)
        {
            var sampleData = GetSampleData(packet, _decoder, _sampleRate,
                _channelCount);

            // TODO: resample and re-encode data

            return false;

            static unsafe float[]? GetSampleData(ReadOnlySequence<byte> packet,
                OpusDecoder* st, int sampleRate, int channelCount)
            {
                int frameSize;
                ReadOnlySpan<byte> opusPacket = packet.FirstSpan;

                if (!packet.IsSingleSegment)
                {
                    Span<byte> tmp = stackalloc byte[(int)packet.Length];
                    packet.CopyTo(tmp);

                    opusPacket = MemoryMarshal.CreateReadOnlySpan(
                        ref MemoryMarshal.GetReference(tmp), tmp.Length);
                }

                fixed (byte* data = opusPacket)
                    frameSize = opus_packet_get_samples_per_frame(data,
                        sampleRate);

                var block = ArrayPool<float>.Shared.Rent(
                    frameSize * channelCount);

                var decoded = 0;
                fixed (byte* input = opusPacket)
                fixed (float* output = block)
                    decoded = opus_decode_float(st, input, opusPacket.Length,
                        output, frameSize, 0);

                if (decoded < 0)
                {
                    ArrayPool<float>.Shared.Return(block);
                    return null;
                }

                return block;
            }
        }

        /// <inheritdoc/>
        public unsafe ValueTask DisposeAsync()
        {
            if (_decoder != null)
                opus_decoder_destroy(_decoder);

            _decoder = null;
            return default;
        }
    }
}
