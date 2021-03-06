using System.Composition;
using System.IO.Pipelines;
using Thermite.Codecs;
using Thermite.Transcoders.Mpeg;

using static Thermite.Utilities.ThrowHelpers;

namespace Thermite.Transcoders
{
    /// <summary>
    /// A transcoder factory used for creating MPEG audio transcoders.
    /// </summary>
    [Export(typeof(IAudioTranscoderFactory))]
    public sealed class MpegAudioTranscoderFactory : IAudioTranscoderFactory
    {
        /// <inheritdoc/>
        public IAudioTranscoder GetTranscoder(IAudioCodec codec, PipeReader input)
        {
            if (codec is MpegAudioCodec mpegInfo)
                return new MpegAudioTranscoder(input, mpegInfo);

            ThrowArgumentException(nameof(codec),
                $"Invalid codec passed to " +
                $"{nameof(MpegAudioTranscoderFactory)}");

            return default!;
        }

        /// <inheritdoc/>
        public bool IsSupported(IAudioCodec codec)
            => codec is MpegAudioCodec;
    }
}
