using System;
using System.Threading.Tasks;

namespace Thermite
{
    /// <summary>
    /// Represents a player for a specific guild.
    /// </summary>
    public interface IPlayer
    {
        /// <summary>
        /// Gets a <see cref="TrackInfo"/> struct representing the current
        /// playing track.
        /// /// </summary>
        TrackInfo CurrentTrack { get; }

        /// <summary>
        /// Enqueues any tracks which may be found at the given
        /// <see cref="Uri"/> for playback.
        /// </summary>
        /// <param name="location">
        /// The location where track info may be found.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous completion
        /// of enqueueing one or more tracks.
        /// </returns>
        ValueTask EnqueueAsync(Uri location);

        /// <summary>
        /// Enqueues a specific <see cref="TrackInfo"/> for playback.
        /// </summary>
        /// <param name="track">
        /// The track to enqueue for playback.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous completion
        /// of enqueueing the track.
        /// </returns>
        ValueTask EnqueueAsync(TrackInfo track);
    }
}
