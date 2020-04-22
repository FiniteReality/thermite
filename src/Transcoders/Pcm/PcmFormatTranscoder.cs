using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Thermite.Codecs;

using static Thermite.Utilities.FrameParsingUtilities;

namespace Thermite.Transcoders.Pcm
{
    internal sealed class PcmFormatTranscoder : IAudioTranscoder
    {
        private readonly PipeReader _input;
        private readonly PcmAudioCodec _codec;
        private readonly Pipe _pipe;

        public PipeReader Output => _pipe.Reader;

        internal PcmFormatTranscoder(PipeReader input,
            PcmAudioCodec codec)
        {
            _input = input;
            _codec = codec;
            _pipe = new Pipe();
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
        }

        public ValueTask<IAudioCodec> GetOutputCodecAsync(
            CancellationToken cancellationToken = default)
            => new ValueTask<IAudioCodec>(
                new PcmAudioCodec(
                    _codec.BitDepth,
                    _codec.ChannelCount,
                    _codec.Endianness,
                    SampleFormat.SignedInteger,
                    _codec.SamplingRate));

        public ValueTask DisposeAsync()
            => default;
    }
}
