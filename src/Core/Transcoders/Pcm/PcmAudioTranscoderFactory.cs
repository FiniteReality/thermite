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
            if (codec is PcmAudioCodec pcmInfo)
                return new PcmAudioTranscoder(input, pcmInfo);

            ThrowArgumentException(nameof(codec),
                $"Invalid codec passed to " +
                $"{nameof(PcmAudioTranscoderFactory)}");

            return default;
        }

        /// <inheritdoc/>
        public bool IsSupported(IAudioCodec codec)
        {
            return codec is PcmAudioCodec;
        }
    }
}
