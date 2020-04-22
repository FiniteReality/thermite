using System.Composition;
using System.IO.Pipelines;
using Thermite.Decoders.Wave;

namespace Thermite.Decoders
{
    /// <summary>
    /// A decoder factory for decoding RIFF WAVE files to their underlying
    /// sample data.
    /// </summary>
    [Export(typeof(IAudioDecoderFactory))]
    public sealed class WaveAudioDecoderFactory : IAudioDecoderFactory
    {
        /// <inheritdoc/>
        public IAudioDecoder GetDecoder(PipeReader input)
            => new WaveAudioDecoder(input);

        /// <inheritdoc/>
        public bool IsSupported(string mediaType)
        {
            return mediaType.Contains("audio/vnd.wave")
                || mediaType.Contains("audio/wav")
                || mediaType.Contains("audio/wave")
                || mediaType.Contains("audio/x-wav");
        }
    }
}
