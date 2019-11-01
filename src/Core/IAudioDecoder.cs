using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Thermite.Core
{
    /// <summary>
    /// An interface used to retrieve audio frames from container files.
    /// </summary>
    public interface IAudioDecoder : IAsyncDisposable
    {
        /// <summary>
        /// A <see cref="PipeReader"/> used to read audio frames from the
        /// decoder.
        /// </summary>
        PipeReader Output { get; }

        /// <summary>
        /// Asynchronously runs the decoder, writing data to
        /// <see cref="Output"/> as it becomes available.
        /// </summary>
        /// <param name="cancellationToken">
        /// The token to monitor for cancellation requests. The default value
        /// is <see cref="CancellationToken.None"/>
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> whose completion signals the completion of
        /// <see cref="Output"/>.
        /// </returns>
        Task RunAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously identifies the codec used to contain audio samples.
        /// </summary>
        /// <param name="cancellationToken">
        /// The token to monitor for cancellation requests. The default value
        /// is <see cref="CancellationToken.None"/>
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous
        /// completion of identifying the audio codec.
        /// </returns>
        ValueTask<string?> IdentifyCodecAsync(
            CancellationToken cancellationToken = default);
    }
}
