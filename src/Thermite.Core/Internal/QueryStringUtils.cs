using System;

namespace Thermite.Core
{
    internal static class QueryStringUtils
    {
        public static bool TryDecode(ReadOnlySpan<byte> source,
            Span<byte> destination, out int decodedBytes)
        {
            if (destination.Length < source.Length)
                throw new ArgumentException(
                    "The destination span is shorter than the source span.",
                    nameof(destination));

            source.CopyTo(destination);
            return TryDecodeInPlace(destination.Slice(0, source.Length),
                out decodedBytes);
        }

        public static bool TryDecodeInPlace(Span<byte> buffer,
            out int decodedBytes)
        {
            decodedBytes = 0;

            int readHead = 0, writeHead = 0;

            while (readHead < buffer.Length)
            {
                switch(buffer[readHead])
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
        }

        private static bool TryPercentDecodeUtf8(Span<byte> buffer,
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

        private static bool TryHexDecode(Span<byte> buffer, out byte hexByte)
        {
            hexByte = 0;

            for (int i = 0; i < 2; i++)
            {
                var shift = (1-i) * 4;

                if (buffer[i] >= (byte)'0' && buffer[i] <= (byte)'9')
                    hexByte |= (byte)((buffer[i] - (byte)'0') << shift);

                else if (buffer[i] >= (byte)'A' && buffer[i] <= (byte)'F')
                    hexByte |= (byte)((buffer[i] - (byte)'A' + 0xA) << shift);

                else if (buffer[i] >= (byte)'a' && buffer[i] <= (byte)'f')
                    hexByte |= (byte)((buffer[i] - (byte)'a' + 0xA) << shift);

                else
                    return false;
            }

            return true;
        }
    }
}
