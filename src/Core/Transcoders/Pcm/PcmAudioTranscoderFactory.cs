using System.Diagnostics;
using System.IO.Pipelines;
using Thermite.Codecs;
using Thermite.Transcoders.Pcm;

using static Thermite.Internal.ThrowHelpers;

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
                // formats supported by discord/libopus
                { ChannelCount: 2,
                  SamplingRate: 48000,
                  BitDepth: 16,
                  Format: SampleFormat.SignedInteger,
                  Endianness: SampleEndianness.LittleEndian }
                    => new PcmEncodingTranscoder<short>(input, pcmCodec),
                { ChannelCount: 2,
                  SamplingRate: 48000,
                  BitDepth: 32,
                  Format: SampleFormat.FloatingPoint,
                  Endianness: SampleEndianness.Indeterminate }
                    => new PcmEncodingTranscoder<float>(input, pcmCodec),

                // explicitly unsupported formats
                // TODO: signed 24-bit audio has a strange encoding
                { BitDepth: 24,
                  Format: SampleFormat.SignedInteger }
                    => InvalidCodec(), // not supported
                // N.B. unsigned 32-bit audio has floating point error
                { BitDepth: 32,
                  Format: SampleFormat.UnsignedInteger }
                    => InvalidCodec(),

                // any big-endian integer data to little endian
                // N.B. use unsigned type params here to allow for this to be a
                // simple byte swap.
                { Endianness: SampleEndianness.BigEndian,
                  Format: SampleFormat.SignedInteger,
                  BitDepth: 16 }
                    => new PcmEndiannessTranscoder<ushort>(input, pcmCodec),
                { Endianness: SampleEndianness.BigEndian,
                  Format: SampleFormat.UnsignedInteger,
                  BitDepth: 16 }
                    => new PcmEndiannessTranscoder<ushort>(input, pcmCodec),
                { Endianness: SampleEndianness.BigEndian,
                  Format: SampleFormat.SignedInteger,
                  BitDepth: 32 }
                    => new PcmEndiannessTranscoder<uint>(input, pcmCodec),

                // any little-endian integer data to float
                { Format: SampleFormat.UnsignedInteger,
                  BitDepth: 8 }
                    => new PcmFloatTranscoder<byte>(input, pcmCodec),
                { Format: SampleFormat.SignedInteger,
                  BitDepth: 8 }
                    => new PcmFloatTranscoder<sbyte>(input, pcmCodec),
                { Format: SampleFormat.SignedInteger,
                  BitDepth: 16 }
                    => new PcmFloatTranscoder<short>(input, pcmCodec),
                { Format: SampleFormat.UnsignedInteger,
                  BitDepth: 16 }
                    => new PcmFloatTranscoder<ushort>(input, pcmCodec),
                { Format: SampleFormat.SignedInteger,
                  BitDepth: 32 }
                    => new PcmFloatTranscoder<int>(input, pcmCodec),

                // any channel count to stereo
                { ChannelCount: 1 }
                    => new PcmChannelDuplicationTranscoder(input, pcmCodec),
                { ChannelCount: var channels }
                    when channels > 2
                    => new PcmDownmixingTranscoder(input, pcmCodec),

                // any sampling rate to 48khz
                { SamplingRate: var rate }
                    when rate != 48000
                    => new PcmResamplingTranscoder(input, pcmCodec),

                // any not explicitly supported configuration
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
