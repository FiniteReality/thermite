using System.IO.Pipelines;

namespace Thermite
{
    /// <summary>
    /// A factory which can be used to create instances of
    /// <see cref="IAudioTranscoder"/>.
    /// </summary>
    public interface IAudioTranscoderFactory
    {
        /// <summary>
        /// Checks whether the given media type is supported by this
        /// transcoder.
        /// </summary>
        /// <param name="codec">
        /// The codec type to test for support.
        /// </param>
        /// <returns>
        /// <code>true</code> if <paramref name="codec"/> is supported by
        /// this transcoder, <code>false</code> otherwise.
        /// </returns>
        bool IsSupported(IAudioCodec codec);

        /// <summary>
        /// Gets a transcoder for the given <see cref="PipeReader"/>.
        /// </summary>
        /// <param name="codec">
        /// The identified codec, which may contain information useful for
        /// transcoding audio data.
        /// </param>
        /// <param name="input">
        /// The input reader returning raw sample data.
        /// </param>
        /// <returns>
        /// A <see cref="IAudioTranscoder"/> containing the state of the
        /// transcoder.
        /// </returns>
        IAudioTranscoder GetTranscoder(IAudioCodec codec, PipeReader input);
    }
}
