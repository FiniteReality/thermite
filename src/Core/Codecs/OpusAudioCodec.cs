namespace Thermite.Codecs
{
    /// <summary>
    /// Contains information about Opus-encoded audio tracks.
    /// </summary>
    public class OpusAudioCodec : IAudioCodec
    {
        /// <inheritdoc/>
        public string Name => "Opus";

        /// <summary>
        /// Initializes a new instance of the <see cref="OpusAudioCodec"/>
        /// class.
        /// </summary>
        /// <param name="sampleRate">
        /// The sample rate, in hertz, to specify for the codec.
        /// </param>
        /// <param name="channelCount">
        /// The number of channels to specify for the codec.
        /// </param>
        /// <param name="bitDepth">
        /// The bit depth to specify for the codec.
        /// </param>
        public OpusAudioCodec(int sampleRate, int channelCount, int bitDepth)
        {
            SamplingRate = sampleRate;
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
