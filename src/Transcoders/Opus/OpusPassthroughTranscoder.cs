using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Thermite.Codecs;

namespace Thermite.Transcoders.Opus
{
    internal sealed class OpusPassthroughTranscoder : IAudioTranscoder
    {
        public PipeReader Output { get; }

        internal OpusPassthroughTranscoder(PipeReader input)
        {
            Output = input;
        }

        public Task RunAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(cancellationToken);

            return Task.CompletedTask;
        }

        public ValueTask<IAudioCodec> GetOutputCodecAsync(
            CancellationToken cancellationToken = default)
            => new ValueTask<IAudioCodec>(
                OpusAudioCodec.DiscordCompatibleOpus);

        public ValueTask DisposeAsync()
        {
            return default;
        }
    }
}
