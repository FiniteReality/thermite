using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;

namespace Thermite.Utilities
{
    internal static class IPUtilities
    {
        public static bool TryParseAddress(ReadOnlySequence<byte> sequence,
            [NotNullWhen(true)]
            out IPAddress? address)
        {
            if (!sequence.IsSingleSegment)
                return SlowPath(sequence, out address);

            return TryParseAddress(sequence.FirstSpan, out address);

            static bool SlowPath(ReadOnlySequence<byte> sequence,
                [NotNullWhen(true)]
                out IPAddress? address)
            {
                Span<byte> buffer = stackalloc byte[(int)sequence.Length];
                sequence.CopyTo(buffer);

                return TryParseAddress(buffer, out address);
            }
        }

        public static bool TryParseAddress(ReadOnlySpan<byte> buffer,
            [NotNullWhen(true)]
            out IPAddress? address)
        {
            address = default!;

            var numChars = Encoding.UTF8.GetCharCount(buffer);
            Span<char> chars = stackalloc char[numChars];
            if (Encoding.UTF8.GetChars(buffer, chars) < 0)
                return false;

            return IPAddress.TryParse(chars, out address);
        }
    }
}
