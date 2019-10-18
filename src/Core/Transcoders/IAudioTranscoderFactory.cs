using System.IO.Pipelines;

namespace Thermite.Core
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
        /// <param name="mediaType">
        /// The media type to test.
        /// </param>
        /// <returns>
        /// <code>true</code> if <paramref name="mediaType"/> is supported by
        /// this transcoder, <code>false</code> otherwise.
        /// </returns>
        bool IsSupported(string mediaType);

        /// <summary>
        /// Gets a transcoder for the given <see cref="PipeReader"/>.
        /// </summary>
        /// <param name="input">
        /// The input reader returning raw sample data.
        /// </param>
        /// <returns>
        /// A <see cref="IAudioTranscoder"/> containing the state of the
        /// transcoder.
        /// </returns>
        IAudioTranscoder GetTranscoder(PipeReader input);
    }
}