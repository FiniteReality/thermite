using System.Composition;
using System.IO.Pipelines;
using Thermite.Decoders.Mpeg;

namespace Thermite.Decoders
{
    /// <summary>
    /// A decoder factory for decoding MPEG 1, 2 and 3 files to their
    /// underlying sample data.
    /// </summary>
    [Export(typeof(IAudioDecoderFactory))]
    public sealed class MpegAudioDecoderFactory : IAudioDecoderFactory
    {
        /// <inheritdoc/>
        public IAudioDecoder GetDecoder(PipeReader input)
        {
            return new MpegAudioDecoder(input);
        }

        /// <inheritdoc/>
        public bool IsSupported(string mediaType)
        {
            return mediaType.Contains("audio/mpeg");
        }
    }
}
