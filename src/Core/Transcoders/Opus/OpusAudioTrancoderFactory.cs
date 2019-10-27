using System.IO.Pipelines;
using Thermite.Core.Transcoders.Opus;

namespace Thermite.Core.Transcoders
{
    /// <summary>
    /// A transcoder factory used for creating instances of
    /// <see cref="OpusAudioTranscoder"/>
    /// </summary>
    public sealed class OpusAudioTranscoderFactory : IAudioTranscoderFactory
    {
        /// <inheritdoc/>
        public IAudioTranscoder GetTranscoder(string codecType,
            PipeReader input)
        {
            return new OpusAudioTranscoder(codecType, input);
        }

        /// <inheritdoc/>
        public bool IsSupported(string codecType)
        {
            return codecType.StartsWith(KnownAudioCodecs.Opus);
        }
    }
}