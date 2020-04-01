using System.Diagnostics;
using System.IO.Pipelines;
using Thermite.Codecs;
using Thermite.Transcoders.Pcm;

using static Thermite.Utilities.ThrowHelpers;

namespace Thermite.Transcoders
{
    /// <summary>
    /// A transcoder factory used for creating PCM transcoders.
    /// </summary>
    public sealed class PcmAudioTranscoderFactory : IAudioTranscoderFactory
    {
        /// <inheritdoc/>
        public IAudioTranscoder GetTranscoder(IAudioCodec codec,
            PipeReader input)
        {
            if (!(codec is PcmAudioCodec pcmCodec))
            {
                ThrowArgumentException(nameof(codec),
                    $"Invalid codec passed to " +
                    $"{nameof(PcmAudioTranscoderFactory)}");

                return default;
            }

            return pcmCodec switch
            {
                { ChannelCount: 2, SamplingRate: 48000, BitDepth: 16,
                  Format: SampleFormat.SignedInteger,
                  Endianness: SampleEndianness.LittleEndian }
                    => new PcmEncodingTranscoder(input, pcmCodec),

                { Endianness: var endian } when
                    endian != SampleEndianness.LittleEndian
                    => new PcmEndiannessTranscoder(input, pcmCodec),

                { Format: var format } when
                    format != SampleFormat.SignedInteger
                    => new PcmFormatTranscoder(input, pcmCodec),

                { BitDepth: var bitDepth } when
                    bitDepth != 16
                    => new PcmBitDepthTranscoder(input, pcmCodec),

                { SamplingRate: var samplingRate } when
                    samplingRate != 48000
                    => new PcmResamplingTranscoder(input, pcmCodec),

                { ChannelCount: 1 }
                    => new PcmStereoTranscoder(input, pcmCodec),
                { ChannelCount : _ }
                    => new PcmDownmixingTranscoder(input, pcmCodec),

                _ => InvalidCodec()
            };

            static IAudioTranscoder InvalidCodec()
            {
                Debug.Fail("Invalid codec properties");

                return null;
            }
        }

        /// <inheritdoc/>
        public bool IsSupported(IAudioCodec codec)
        {
            return codec is PcmAudioCodec;
        }
    }
}
