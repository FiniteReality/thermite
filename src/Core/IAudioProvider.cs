using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Thermite
{
    /// <summary>
    /// An interface used to provide audio samples for transcoding.
    /// </summary>
    public interface IAudioProvider : IAsyncDisposable
    {
        /// <summary>
        /// A <see cref="PipeReader"/> used to read samples from the underlying
        /// provider.
        /// </summary>
        PipeReader Output { get; }

        /// <summary>
        /// Asynchronously runs the provider, writing data to
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
        /// Asynchronously identifies the media type of the provided audio
        /// file.
        /// </summary>
        /// <param name="cancellationToken">
        /// The token to monitor for cancellation requests. The default value
        /// is <see cref="CancellationToken.None" />
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous
        /// completion of identifying the media type.
        /// </returns>
        ValueTask<string> IdentifyMediaTypeAsync(
            CancellationToken cancellationToken = default);
    }
}
