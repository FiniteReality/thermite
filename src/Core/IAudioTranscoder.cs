using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Thermite
{
    /// <summary>
    /// An interface used to transcode audio data into Thermite-compatible
    /// Opus packets.
    /// </summary>
    public interface IAudioTranscoder : IAsyncDisposable
    {
        /// <summary>
        /// A <see cref="PipeReader"/> used to read packets from the underlying
        /// transcoder.
        /// </summary>
        PipeReader Output { get; }

        /// <summary>
        /// Asynchronously runs the transcoder, writing data to
        /// <see cref="Output"/> as it becomes available.
        /// </summary>
        /// <param name="cancellationToken">
        /// The token to monitor for cancellation requests. The default value
        /// is <see cref="CancellationToken.None" />
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> whose completion signals the completion of
        /// <see cref="Output"/>.
        /// </returns>
        Task RunAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously identifies the output codec of audio data returned
        /// by this transcoder.
        /// </summary>
        /// <param name="cancellationToken">
        /// The token to monitor for cancellation requests. The default value
        /// is <see cref="CancellationToken.None"/>
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous
        /// completion of identifying the audio codec.
        /// </returns>
        ValueTask<IAudioCodec> GetOutputCodecAsync(
            CancellationToken cancellationToken = default);
    }
}
