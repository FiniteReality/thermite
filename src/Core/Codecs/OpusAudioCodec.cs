namespace Thermite.Codecs
{
    /// <summary>
    /// Contains information about Opus-encoded audio tracks.
    /// </summary>
    public class OpusAudioCodec : IAudioCodec
    {
        /// <summary>
        /// Gets a <see cref="OpusAudioCodec"/> representing codec properties
        /// which are compatible with Discord.
        /// </summary>
        public static OpusAudioCodec DiscordCompatibleOpus { get; }
            = new OpusAudioCodec(
                samplingRate: 48000,
                channelCount: 2,
                bitDepth: sizeof(short) * 8);

        /// <inheritdoc/>
        public string Name => "Opus";

        /// <summary>
        /// Initializes a new instance of the <see cref="OpusAudioCodec"/>
        /// class.
        /// </summary>
        /// <param name="samplingRate">
        /// The sample rate, in hertz, to specify for the codec.
        /// </param>
        /// <param name="channelCount">
        /// The number of channels to specify for the codec.
        /// </param>
        /// <param name="bitDepth">
        /// The bit depth to specify for the codec.
        /// </param>
        public OpusAudioCodec(int samplingRate, int channelCount, int bitDepth)
        {
            SamplingRate = samplingRate;
            ChannelCount = channelCount;
            BitDepth = bitDepth;
        }

        /// <inheritdoc/>
        public int BitDepth { get; }

        /// <inheritdoc/>
        public int ChannelCount { get; }

        /// <inheritdoc/>
        public int SamplingRate { get; }
    }
}
