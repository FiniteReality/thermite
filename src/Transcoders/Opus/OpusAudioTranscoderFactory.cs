using System.Composition;
using System.Diagnostics;
using System.IO.Pipelines;
using Thermite.Codecs;
using Thermite.Transcoders.Opus;

using static Thermite.Utilities.ThrowHelpers;

namespace Thermite.Transcoders
{
    /// <summary>
    /// A transcoder factory used for creating Opus transcoders and resamplers.
    /// </summary>
    [Export(typeof(IAudioTranscoderFactory))]
    public sealed class OpusAudioTranscoderFactory : IAudioTranscoderFactory
    {
        /// <inheritdoc/>
        public IAudioTranscoder GetTranscoder(IAudioCodec codec,
            PipeReader input)
        {
            if (!(codec is OpusAudioCodec opusCodec))
            {
                ThrowArgumentException(nameof(codec),
                    $"Invalid codec passed to " +
                    $"{nameof(OpusAudioTranscoderFactory)}");

                return default;
            }

            return opusCodec switch
            {
                { ChannelCount: 2, SamplingRate: 48000 }
                    => new OpusPassthroughTranscoder(input),

                { ChannelCount: _, SamplingRate: _ }
                    => new OpusDecodingTranscoder(input, opusCodec),

                _ => InvalidCodec()
            };

            static IAudioTranscoder InvalidCodec()
            {
                Debug.Assert(false, "Invalid codec properties");

                return null;
            }
        }

        /// <inheritdoc/>
        public bool IsSupported(IAudioCodec codec)
            => codec is OpusAudioCodec;
    }
}
