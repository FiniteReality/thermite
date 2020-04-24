using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;

namespace Thermite.Utilities
{
    internal static class Utf8IpAddressUtilities
    {
        public static bool TryParseAddress(ReadOnlySpan<byte> buffer,
            [NotNullWhen(true)]
            out IPAddress? address)
        {
            address = default!;

            var numChars = Encoding.UTF8.GetCharCount(buffer);
            Span<char> chars = stackalloc char[numChars];

            return Encoding.UTF8.GetChars(buffer, chars) >= 0
                && IPAddress.TryParse(chars, out address);
        }
    }
}
