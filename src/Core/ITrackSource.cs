using System;
using System.Collections.Generic;
using System.Threading;

namespace Thermite
{
    /// <summary>
    /// Represents a source for locating audio tracks on the internet.
    /// </summary>
    public interface ITrackSource
    {
        /// <summary>
        /// Checks whether the given source supports the given
        /// <see cref="Uri"/>.
        /// </summary>
        /// <param name="location">The location of the track info.</param>
        /// <returns><code>true</code> if the given URI is supported.</returns>
        bool IsSupported(Uri location);

        /// <summary>
        /// Gets any tracks which can be found at the given location.
        /// </summary>
        /// <param name="location">
        /// The location to retrieve track info from.
        /// </param>
        /// <param name="cancellationToken">
        /// The token to monitor for cancellation requests. The default value
        /// is <see cref="CancellationToken.None" />
        /// </param>
        /// <returns>
        /// An asynchronous enumerable, representing track info as they are
        /// identified.
        /// </returns>
        IAsyncEnumerable<TrackInfo> GetTracksAsync(Uri location,
            CancellationToken cancellationToken = default);
    }
}
