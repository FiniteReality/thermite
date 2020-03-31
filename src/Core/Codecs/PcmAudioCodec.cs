namespace Thermite.Codecs
{
    /// <summary>
    /// Contains information about PCM-encoded audio tracks.
    /// </summary>
    public class PcmAudioCodec : IAudioCodec
    {
        /// <inheritdoc/>
        public string Name => "PCM";

        /// <summary>
        /// Initializes a new instance of the <see cref="PcmAudioCodec"/>
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
        public PcmAudioCodec(int sampleRate, int channelCount, int bitDepth)
        {
            SamplingRate = sampleRate;
            ChannelCount = channelCount;
            BitDepth = bitDepth;
        }

        /// <inheritdoc/>
        public int SamplingRate { get; }

        /// <inheritdoc/>
        public int ChannelCount { get; }

        /// <inheritdoc/>
        public int BitDepth { get; }
    }
}
