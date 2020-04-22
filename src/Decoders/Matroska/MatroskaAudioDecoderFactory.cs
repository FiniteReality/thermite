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
            => new MatroskaAudioDecoder(input);

        /// <inheritdoc/>
        public bool IsSupported(string mediaType)
        {
            return mediaType.StartsWith("video/webm")
                || mediaType.StartsWith("audio/webm")
                || mediaType.StartsWith("video/x-matroska")
                || mediaType.StartsWith("audio/x-matroska");
        }
    }
}
