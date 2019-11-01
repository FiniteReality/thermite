using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Thermite.Core.Transcoders.Opus
{
    /// <summary>
    /// A transcoder which performs no operation on the audio data as it passes
    /// through.
    /// /// </summary>
    public sealed class OpusAudioPassthroughTranscoder : IAudioTranscoder
    {
        /// <inheritdoc/>
        public PipeReader Output { get; }

        internal OpusAudioPassthroughTranscoder(PipeReader input)
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
        public ValueTask DisposeAsync()
        {
            return default;
        }
    }
}
