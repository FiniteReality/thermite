using System;
using System.IO.Pipelines;

namespace Thermite
{
    /// <summary>
    /// A factory which can be used to create instances of
    /// <see cref="IAudioDecoder"/>.
    /// </summary>
    public interface IAudioDecoderFactory
    {
        /// <summary>
        /// Checks whether the given media type is supported by this
        /// decoder.
        /// </summary>
        /// <param name="mediaType">
        /// The media type to test.
        /// </param>
        /// <returns>
        /// <code>true</code> if <paramref name="mediaType"/> is supported by
        /// this decoder, <code>false</code> otherwise.
        /// </returns>
        bool IsSupported(string mediaType);

        /// <summary>
        /// Gets a decoder for the given <see cref="PipeReader"/>.
        /// </summary>
        /// <param name="input">
        /// The input reader returning raw container data.
        /// </param>
        /// <returns>
        /// A <see cref="IAudioDecoder"/> containing the state of the decoder.
        /// </returns>
        IAudioDecoder GetDecoder(PipeReader input);
    }
}
