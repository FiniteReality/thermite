using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Thermite.Core.Transcoders.Opus
{
    /// <summary>
    /// A transcoder which transcodes Opus packets to Discord-compatible Opus
    /// packets.
    /// </summary>
    public sealed class OpusAudioTranscoder : IAudioTranscoder
    {
        /// <inheritdoc/>
        public PipeReader Output { get; }

        internal OpusAudioTranscoder(string codecInfo, PipeReader input)
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