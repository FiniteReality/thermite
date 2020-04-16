using System.Buffers;

namespace Thermite.Utilities
{
    /// <summary>
    /// Provides a set of methods for parsing Thermite audio frames.
    /// </summary>
    public static class FrameParsingUtilities
    {
        /// <summary>
        /// Attempts to read a Thermite audio frame from the given
        /// <see cref="ReadOnlySequence{T}"/>.
        /// </summary>
        /// <remarks>
        /// The <paramref name="sequence"/> parameter is updated to point at
        /// the first byte after the end of the frame, so that this method can
        /// be called as the condition of a while loop.
        /// </remarks>
        /// <param name="sequence">
        /// The sequence to read a frame from.
        /// </param>
        /// <param name="frame">
        /// The decoded frame contents.
        /// </param>
        /// <returns>
        /// Returns <code>true</code> when a valid frame was read, and
        /// <code>false</code> otherwise.
        /// </returns>
        public static bool TryReadFrame(
            ref ReadOnlySequence<byte> sequence,
            out ReadOnlySequence<byte> frame)
        {
            frame = default;
            var reader = new SequenceReader<byte>(sequence);

            if (!reader.TryReadLittleEndian(out short frameLength))
                return false;

            if (sequence.Length < frameLength)
                return false;

            frame = sequence.Slice(reader.Position, frameLength);
            var nextFrame = sequence.GetPosition(frameLength,
                reader.Position);
            sequence = sequence.Slice(nextFrame);
            return true;
        }
    }
}
