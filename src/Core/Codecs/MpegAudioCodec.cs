namespace Thermite.Codecs
{
    /// <summary>
    /// Contains information about MPEG-encoded audio tracks.
    /// </summary>
    public sealed class MpegAudioCodec : IAudioCodec
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MpegAudioCodec"/>
        /// class.
        /// </summary>
        /// <param name="version">
        /// The MPEG version used for the codec.
        /// </param>
        /// <param name="layer">
        /// The layer used for the codec.
        /// </param>
        /// <param name="sampleRate">
        /// The sample rate, in hertz, to specify for the codec.
        /// </param>
        /// <param name="channelCount">
        /// The number of channels to specify for the codec.
        /// </param>
        /// <param name="bitrate">
        /// The average bitrate, in kilobits per second, used by the codec.
        /// </param>
        public MpegAudioCodec(int version, int layer, int sampleRate,
            int channelCount, int bitrate)
        {
            Version = version;
            Layer = layer;
            SamplingRate = sampleRate;
            ChannelCount = channelCount;
            BitRate = bitrate;

            Name = $"MPEG-{version} Audio Layer {layer}";
        }

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public int BitDepth { get; } = sizeof(short) * 8;

        /// <summary>
        /// The average bitrate, in kilobits per second, used by the codec.
        /// </summary>
        public int BitRate { get; }

        /// <inheritdoc/>
        public int ChannelCount { get; }

        /// <summary>
        /// The MPEG layer used by the codec.
        /// </summary>
        public int Layer { get; }

        /// <inheritdoc/>
        public int SamplingRate { get; }

        /// <summary>
        /// The MPEG version used by the codec.
        /// </summary>
        public int Version { get; }
    }
}
