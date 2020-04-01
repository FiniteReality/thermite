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
    public sealed class PcmAudioTranscoder : IAudioTranscoder
    {
        private readonly PipeReader _input;
        private readonly Pipe _pipe;

        private readonly int _channelCount;

        private unsafe OpusEncoder* _encoder;

        /// <inheritdoc/>
        public PipeReader Output => _pipe.Reader;

        internal unsafe PcmAudioTranscoder(PipeReader input,
            PcmAudioCodec properties)
        {
            const int DesiredSampleRate = 48000;
            const int DesiredBitDepth = 16;
            const int Application = 2049; // OPUS_APPLICATION_AUDIO

            if (properties.SamplingRate != DesiredSampleRate)
                ThrowArgumentOutOfRangeException(nameof(properties),
                    properties,
                    $"{nameof(PcmAudioTranscoder)} does not support streams " +
                    $"with sampling rates other than {DesiredSampleRate} Hz.");

            if (properties.BitDepth != DesiredBitDepth)
                ThrowArgumentOutOfRangeException(nameof(properties),
                    properties,
                    $"{nameof(PcmAudioTranscoder)} does not support streams " +
                    $"with bit depths other than {DesiredBitDepth}.");

            _channelCount = properties.ChannelCount;

            int status;
            _encoder = opus_encoder_create(DesiredSampleRate, _channelCount,
                Application, &status);

            if (status < 0)
                ThrowExternalException("Could not create Opus Encoder",
                    status);

            _input = input;
            _pipe = new Pipe();
        }

        /// <summary>
        /// Finalizes an instance of <see cref="PcmAudioTranscoder"/>.
        /// </summary>
        ~PcmAudioTranscoder()
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

                        var encoded = TryWritePacket(frame, writer);
                        Debug.Assert(encoded > 0, "Opus packet encode error");

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

                if (!reader.TryReadLittleEndian(out short packetLength))
                    return false;

                if (sequence.Length < packetLength)
                    return false;

                frame = sequence.Slice(reader.Position, packetLength);
                var nextPacket = sequence.GetPosition(packetLength,
                    reader.Position);
                sequence = sequence.Slice(nextPacket);
                return true;
            }
        }

        private unsafe int TryWritePacket(ReadOnlySequence<byte> packet,
            PipeWriter writer)
        {
            if (packet.IsSingleSegment)
                return WriteInternal(_encoder, _channelCount, packet.FirstSpan,
                    writer);

            var buffer = ArrayPool<byte>.Shared.Rent((int)packet.Length);
            packet.CopyTo(buffer);

            var status = WriteInternal(_encoder, _channelCount,
                buffer.AsSpan().Slice(0, (int)packet.Length),
                writer);

            ArrayPool<byte>.Shared.Return(buffer);

            return status;

            static unsafe int WriteInternal(OpusEncoder* encoder,
                int channelCount, ReadOnlySpan<byte> packet, PipeWriter writer)
            {
                var block = writer.GetSpan();

                int frameSize = packet.Length / sizeof(short) / channelCount;

                int encoded;
                fixed (byte* sampleData = packet)
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
