using System.Buffers;

namespace Thermite.Utilities
{
    internal static class FrameParsingUtilities
    {
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
