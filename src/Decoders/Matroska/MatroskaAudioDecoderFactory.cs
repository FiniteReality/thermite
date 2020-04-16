using System.Composition;
using System.IO.Pipelines;
using Thermite.Decoders.Matroska;

namespace Thermite.Decoders
{
    /// <summary>
    /// A decoder factory for decoding Matroska and WEBM files to their
    /// underlying frames.
    /// </summary>
    [Export(typeof(IAudioDecoderFactory))]
    public sealed class MatroskaAudioDecoderFactory
        : IAudioDecoderFactory
    {
        /// <inheritdoc/>
        public IAudioDecoder GetDecoder(PipeReader input)
        {
            return new MatroskaAudioDecoder(input);
        }

        /// <inheritdoc/>
        public bool IsSupported(string mediaType)
        {
            return mediaType.Contains("video/webm")
                || mediaType.Contains("audio/webm")
                || mediaType.Contains("video/x-matroska")
                || mediaType.Contains("audio/x-matroska");
        }
    }
}
