using System;
using System.Buffers;
using System.Diagnostics;

namespace Thermite.Utilities
{
    internal static class TextParsingUtilities
    {
        [DebuggerStepThrough]
        public static bool TryGetKeyValuePair(
            ref ReadOnlySequence<byte> sequence,
            out ReadOnlySequence<byte> key, out ReadOnlySequence<byte> value)
        {
            if (sequence.IsEmpty)
            {
                key = default;
                value = default;
                return false;
            }

            value = default;

            if (TryReadTo(ref sequence, (byte)'=', out key) &&
                TryReadTo(ref sequence, (byte)'&', out value))
                return true;

            return false;
        }

        [DebuggerStepThrough]
        public static bool TryGetKeyValuePair(ref ReadOnlySpan<byte> input,
            out ReadOnlySpan<byte> key, out ReadOnlySpan<byte> value)
        {
            if (input.IsEmpty)
            {
                key = default;
                value = default;
                return false;
            }

            value = default;

            if (TryReadTo(ref input, (byte)'=', out key) &&
                TryReadTo(ref input, (byte)'&', out value))
                return true;

            return false;
        }

        [DebuggerStepThrough]
        public static bool TryReadTo(ref ReadOnlySequence<byte> input,
            byte separator, out ReadOnlySequence<byte> skipped)
        {
            if (input.IsEmpty)
            {
                skipped = default;
                return false;
            }

            var position = input.PositionOf(separator);

            if (position == null)
            {
                skipped = input;
                input = ReadOnlySequence<byte>.Empty;
                return true;
            }

            skipped = input.Slice(input.Start, position.Value);
            input = input.Slice(input.GetPosition(1, position.Value));
            return true;
        }

        [DebuggerStepThrough]
        public static bool TryReadTo(ref ReadOnlySpan<byte> input,
            byte separator, out ReadOnlySpan<byte> skipped)
        {
            if (input.IsEmpty)
            {
                skipped = default;
                return false;
            }

            var position = input.IndexOf(separator);

            if (position < 0)
            {
                skipped = input;
                input = ReadOnlySpan<byte>.Empty;
                return true;
            }

            skipped = input.Slice(0, position);
            input = input.Slice(position + 1);
            return true;
        }

        [DebuggerStepThrough]
        public static bool SequenceEqual(ReadOnlySequence<byte> first,
            ReadOnlySpan<byte> second)
        {
            if (first.IsEmpty && second.IsEmpty)
                return true;

            if (first.Length != second.Length)
                return false;

            if (first.IsSingleSegment)
                return first.FirstSpan.SequenceEqual(second);

            Span<byte> buffer = stackalloc byte[second.Length];
            first.CopyTo(buffer);

            return buffer.SequenceEqual(second);
        }

        public static bool TryUrlDecode(ReadOnlySpan<byte> source,
            Span<byte> destination, out int decodedBytes)
        {
            decodedBytes = default;
            if (destination.Length < source.Length)
                return false;

            if (!source.TryCopyTo(destination))
                return false;

            return TryUrlDecode(
                destination.Slice(0, source.Length),
                out decodedBytes);
        }

        public static bool TryUrlDecode(Span<byte> buffer,
            out int decodedBytes)
        {
            int readHead = 0, writeHead = 0;

            while (readHead < buffer.Length)
            {
                switch (buffer[readHead])
                {
                    case (byte)'+':
                        buffer[writeHead++] = (byte)' ';
                        break;
                    case (byte)'%':
                        if (!TryPercentDecodeUtf8(buffer,
                            ref readHead, ref writeHead))
                            buffer[writeHead++] = (byte)'%';
                        readHead -= 1;
                        break;
                    default:
                        buffer[writeHead++] = buffer[readHead];
                        break;
                }

                readHead++;
            }

            decodedBytes = writeHead;
            return true;

            static bool TryPercentDecodeUtf8(Span<byte> buffer,
                ref int readHead, ref int writeHead)
            {
                while (readHead < buffer.Length
                    && buffer[readHead] == (byte)'%')
                {
                    if (!TryHexDecode(buffer.Slice(readHead + 1, 2),
                        out var decodedByte))
                        return false;

                    buffer[writeHead++] = decodedByte;
                    readHead += 3;
                }

                return true;
            }

            static bool TryHexDecode(Span<byte> buffer, out byte hexByte)
            {
                hexByte = 0;

                for (int i = 0; i < 2; i++)
                {
                    var shift = (1 - i) * 4;

                    if (buffer[i] >= (byte)'0' && buffer[i] <= (byte)'9')
                        hexByte |= (byte)((buffer[i] - (byte)'0') << shift);

                    else if (buffer[i] >= (byte)'A' && buffer[i] <= (byte)'F')
                        hexByte |= (byte)(
                            (buffer[i] - (byte)'A' + 0xA) << shift);

                    else if (buffer[i] >= (byte)'a' && buffer[i] <= (byte)'f')
                        hexByte |= (byte)(
                            (buffer[i] - (byte)'a' + 0xA) << shift);

                    else
                        return false;
                }

                return true;
            }
        }
    }
}
