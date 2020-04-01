using System;
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Thermite.Codecs;

using static Thermite.Utilities.ThrowHelpers;

namespace Thermite.Transcoders.Pcm
{
    /// <summary>
    /// A transcoder which encodes PCM sample data to Opus as it passes
    /// through.
    /// </summary>
    public sealed class PcmFormatTranscoder : IAudioTranscoder
    {
        private readonly PipeReader _input;
        private readonly PcmAudioCodec _codec;
        private readonly Pipe _pipe;

        /// <inheritdoc/>
        public PipeReader Output => _pipe.Reader;

        internal PcmFormatTranscoder(PipeReader input,
            PcmAudioCodec codec)
        {
            _input = input;
            _codec = codec;
            _pipe = new Pipe();
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

                        Debug.Fail("Not implemented");
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
                new PcmAudioCodec(
                    _codec.BitDepth,
                    _codec.ChannelCount,
                    _codec.Endianness,
                    SampleFormat.SignedInteger,
                    _codec.SamplingRate));

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            return default;
        }
    }
}
