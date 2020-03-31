namespace Thermite
{
    /// <summary>
    /// Contains information about the codec a given audio track is encoded
    /// using.
    /// </summary>
    public interface IAudioCodec
    {
        /// <summary>
        /// The user-facing name for the codec.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The bit depth of audio samples encoded using the codec.
        /// </summary>
        int BitDepth { get; }

        /// <summary>
        /// The number of channels encoded using the codec.
        /// </summary>
        int ChannelCount { get; }

        /// <summary>
        /// The sampling rate, in Hertz, of audio encoded using the codec.
        /// </summary>
        int SamplingRate { get; }
    }
}
