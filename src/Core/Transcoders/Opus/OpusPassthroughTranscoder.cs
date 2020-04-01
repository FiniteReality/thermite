using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Thermite.Codecs;

namespace Thermite.Transcoders.Opus
{
    /// <summary>
    /// A transcoder which performs no operation on the audio data as it passes
    /// through.
    /// /// </summary>
    public sealed class OpusPassthroughTranscoder : IAudioTranscoder
    {
        /// <inheritdoc/>
        public PipeReader Output { get; }

        internal OpusPassthroughTranscoder(PipeReader input)
        {
            Output = input;
        }

        /// <inheritdoc/>
        public Task RunAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(cancellationToken);

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public ValueTask<IAudioCodec> GetOutputCodecAsync(
            CancellationToken cancellationToken = default)
            => new ValueTask<IAudioCodec>(
                OpusAudioCodec.DiscordCompatibleOpus);

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            return default;
        }
    }
}
