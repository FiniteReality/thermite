using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Thermite.Natives;

using static Thermite.Natives.Opus;
using static Thermite.Utilities.ThrowHelpers;

namespace Thermite.Core.Transcoders.Opus
{
    /// <summary>
    /// A transcoder which resamples Opus packets to be Discord-compatible.
    /// </summary>
    public sealed class OpusAudioResamplingTranscoder : IAudioTranscoder
    {
        private readonly PipeReader _input;
        private readonly Pipe _pipe;

        private readonly int _sampleRate;
        private readonly int _channelCount;

        private unsafe OpusDecoder* _decoder;
        private unsafe OpusEncoder* _encoder;

        /// <inheritdoc/>
        public PipeReader Output => _pipe.Reader;

        internal unsafe OpusAudioResamplingTranscoder(PipeReader input,
            int sampleRate, int channelCount)
        {
            ThrowNotImplemented();

            const int DesiredSampleRate = 48000;
            const int Application = 2049; // OPUS_APPLICATION_AUDIO

            _sampleRate = sampleRate;
            _channelCount = channelCount;

            int status;
            _decoder = opus_decoder_create(sampleRate, channelCount, &status);

            if (status < 0)
                ThrowExternalException("Could not create Opus Decoder",
                    status);

            _encoder = opus_encoder_create(DesiredSampleRate, channelCount,
                Application, &status);

            if (status < 0)
                ThrowExternalException("Could not create Opus Encoder",
                    status);

            _input = input;
            _pipe = new Pipe();

            static void ThrowNotImplemented()
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Finalizes an instance of
        /// <see cref="OpusAudioResamplingTranscoder"/>.
        /// </summary>
        ~OpusAudioResamplingTranscoder()
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

                    while (TryReadPacket(ref sequence, out var packet))
                    {
                        if (packet.IsEmpty)
                            continue;

                        if (!TryTranscodePacket(packet, writer))
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

            static bool TryReadPacket(
                ref ReadOnlySequence<byte> sequence,
                out ReadOnlySequence<byte> packet)
            {
                packet = default;
                var reader = new SequenceReader<byte>(sequence);

                if (!reader.TryReadLittleEndian(out short packetLength))
                    return false;

                if (sequence.Length < packetLength)
                    return false;

                packet = sequence.Slice(reader.Position, packetLength);
                sequence = packet.Slice(reader.Position)
                    .Slice(packetLength);
                return true;
            }
        }

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
            if (_encoder != null)
                opus_encoder_destroy(_encoder);

            _decoder = null;
            _encoder = null;
            return default;
        }
    }
}
