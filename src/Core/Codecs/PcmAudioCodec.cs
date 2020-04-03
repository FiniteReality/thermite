using static Thermite.Utilities.ThrowHelpers;

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
        /// <param name="bitDepth">
        /// The bit depth to specify for the codec.
        /// </param>
        /// <param name="channelCount">
        /// The number of channels to specify for the codec.
        /// </param>
        /// <param name="endianness">
        /// The sample endianness of samples for the codec.
        /// </param>
        /// <param name="format">
        /// The sample format of samples for the codec.
        /// </param>
        /// <param name="samplingRate">
        /// The sample rate, in hertz, to specify for the codec.
        /// </param>
        public PcmAudioCodec(int bitDepth, int channelCount,
            SampleEndianness endianness, SampleFormat format, int samplingRate)
        {
            if ((endianness != SampleEndianness.Indeterminate) &&
                (format == SampleFormat.FloatingPoint ||
                 format == SampleFormat.FixedPoint))
                ThrowArgumentException(nameof(endianness),
                    "Fixed or floating point samples cannot have endianness");

            BitDepth = bitDepth;
            ChannelCount = channelCount;
            Endianness = endianness;
            Format = format;
            SamplingRate = samplingRate;
        }

        /// <inheritdoc/>
        public int BitDepth { get; }

        /// <inheritdoc/>
        public int ChannelCount { get; }

        /// <summary>
        /// The endianness of each sample in the audio stream.
        /// </summary>
        public SampleEndianness Endianness { get; }

        /// <summary>
        /// The format of each sample in the audio stream.
        /// </summary>
        public SampleFormat Format { get; }

        /// <inheritdoc/>
        public int SamplingRate { get; }
    }
}
