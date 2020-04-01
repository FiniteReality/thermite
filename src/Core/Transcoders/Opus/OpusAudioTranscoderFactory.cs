using System;
using System.Globalization;
using System.IO.Pipelines;
using Thermite.Codecs;
using Thermite.Transcoders.Opus;

using static Thermite.Utilities.ThrowHelpers;

namespace Thermite.Transcoders
{
    /// <summary>
    /// A transcoder factory used for creating Opus transcoders and resamplers.
    /// </summary>
    public sealed class OpusAudioTranscoderFactory : IAudioTranscoderFactory
    {
        /// <inheritdoc/>
        public IAudioTranscoder GetTranscoder(IAudioCodec codec,
            PipeReader input)
        {
            if (codec is OpusAudioCodec opusCodec)
                return new OpusAudioPassthroughTranscoder(input);

            ThrowArgumentException(nameof(codec),
                $"Invalid codec passed to " +
                $"{nameof(PcmAudioTranscoderFactory)}");

            return default;
        }

        /// <inheritdoc/>
        public bool IsSupported(IAudioCodec codec)
        {
            return false;
        }
    }
}
