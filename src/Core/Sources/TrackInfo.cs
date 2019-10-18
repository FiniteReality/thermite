using System;

namespace Thermite.Core
{
    /// <summary>
    /// Contains information about a track with a uniform locator.
    /// </summary>
    public struct TrackInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TrackInfo"/>
        /// structure.
        /// </summary>
        /// <param name="name">
        /// The name of the track, as indicated by the remote source.
        /// </param>
        /// <param name="originalLocation">
        /// The original location where this track can be found.
        /// </param>
        /// <param name="audioLocation">
        /// The location where any audio/video streams for this track can be
        /// found.
        /// </param>
        public TrackInfo(string name, Uri originalLocation, Uri audioLocation)
        {
            TrackName = name;
            OriginalLocation = originalLocation;
            AudioLocation = audioLocation;
            MediaTypeOverride = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TrackInfo"/>
        /// structure.
        /// </summary>
        /// <param name="name">
        /// The name of the track, as indicated by the remote source.
        /// </param>
        /// <param name="originalLocation">
        /// The original location where this track can be found.
        /// </param>
        /// <param name="audioLocation">
        /// The location where any audio/video streams for this track can be
        /// found.
        /// </param>
        /// <param name="mediaType">
        /// Overrides any media type which may be identified while reading
        /// <paramref name="audioLocation"/>.
        /// </param>
        public TrackInfo(string name, Uri originalLocation, Uri audioLocation,
            string mediaType)
        {
            TrackName = name;
            OriginalLocation = originalLocation;
            AudioLocation = audioLocation;
            MediaTypeOverride = mediaType;
        }


        /// <summary>
        /// The name of the track this <see cref="TrackInfo" /> represents.
        /// </summary>
        public readonly string TrackName;
        /// <summary>
        /// The original location where the track can be found.
        /// </summary>
        public readonly Uri OriginalLocation;
        /// <summary>
        /// The machine-readable location where the track audio can be located.
        /// </summary>
        public readonly Uri AudioLocation;
        /// <summary>
        /// Overrides the media type identified while reading
        /// <see cref="AudioLocation"/>.
        /// </summary>
        public readonly string? MediaTypeOverride;
    }
}