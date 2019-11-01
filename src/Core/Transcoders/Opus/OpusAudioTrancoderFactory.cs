using System;
using System.Globalization;
using System.IO.Pipelines;
using Thermite.Core.Transcoders.Opus;

namespace Thermite.Core.Transcoders
{
    /// <summary>
    /// A transcoder factory used for creating Opus transcoders and resamplers.
    /// </summary>
    public sealed class OpusAudioTranscoderFactory : IAudioTranscoderFactory
    {
        /// <inheritdoc/>
        public IAudioTranscoder GetTranscoder(string codecType,
            PipeReader input)
        {
            if (TryGetResamplingOptions(codecType, out var sampleRate,
                out var channelCount))
            {
                throw new NotImplementedException();
            }

            return new OpusAudioPassthroughTranscoder(input);

            static bool TryGetResamplingOptions(string codecType,
                out int sampleRate, out int channelCount)
            {
                // Min length of resampling options w/o codec name
                const int MinLength = 4;
                sampleRate = default;
                channelCount = default;

                if (codecType.Length <
                    KnownAudioCodecs.Opus.Length + MinLength)
                    return false;

                var span = codecType.AsSpan().Slice(
                    KnownAudioCodecs.Opus.Length + 1);
                var split = span.IndexOf('/');

                if (split < 0)
                    return false;

                if (!int.TryParse(span.Slice(0, split), NumberStyles.Integer,
                    CultureInfo.InvariantCulture.NumberFormat,
                    out sampleRate))
                    return false;

                if (!int.TryParse(span.Slice(split), NumberStyles.Integer,
                    CultureInfo.InvariantCulture.NumberFormat,
                    out channelCount))
                    return false;

                return true;
            }
        }

        /// <inheritdoc/>
        public bool IsSupported(string codecType)
        {
            return codecType.StartsWith(KnownAudioCodecs.Opus);
        }
    }
}
