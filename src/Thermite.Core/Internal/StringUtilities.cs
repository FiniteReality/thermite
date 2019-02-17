using System;
using System.Buffers;
using System.Text;
using Voltaic;

namespace Thermite.Core
{
    internal static class StringUtilities
    {
        public static bool TryReadCommaSeparatedValue(
            ref ReadOnlySpan<byte> span, out ReadOnlySpan<byte> value)
        {
            if (span == default || span.Length == 0)
            {
                value = Span<byte>.Empty;
                return false;
            }

            bool inString = false;
            for (int i = 0; i < span.Length; i++)
            {
                if (span[i] == '"' || span[i] == '\'')
                    inString = !inString;

                if (!inString && span[i] == ',')
                {
                    value = span.Slice(0, i);
                    span = (span.Length - i == 0) ?
                        Span<byte>.Empty : span.Slice(i + 1);
                    return true;
                }
            }

            value = span;
            span = Span<byte>.Empty;
            return true;
        }

        public static bool TryReadUrlEncodedKeyValuePair(
            ref ReadOnlySpan<byte> span, out ReadOnlySpan<byte> key,
            out ReadOnlySpan<byte> value)
        {
            key = Span<byte>.Empty;
            value = Span<byte>.Empty;

            if (span == default || span.Length == 0)
                return false;

            var keyIndex = span.IndexOf((byte)'=');
            if (keyIndex < 0)
                return false;

            key = span.Slice(0, keyIndex);
            span = span.Slice(keyIndex);

            if (span[0] != '=')
                return false;
            span = span.Slice(1);

            var valueIndex = span.IndexOf((byte)'&');
            if (valueIndex < 0)
            {
                value = span;
                span = Span<byte>.Empty;

                return true;
            }

            value = span.Slice(0, valueIndex);
            span = span.Slice(valueIndex + 1);
            return true;
        }

        public static unsafe string GetString(this Encoding encoding,
            ReadOnlySpan<byte> input)
        {
            fixed (byte* bytes = &input.GetPinnableReference())
                return encoding.GetString(bytes, input.Length);
        }

        public static bool TryUrlDecode(ReadOnlySpan<byte> source,
            out ResizableMemory<byte> memory, ArrayPool<byte> pool = null)
        {
            memory = new ResizableMemory<byte>(source.Length, pool);

            if (!QueryStringUtils.TryDecode(source,
                memory.RequestSpan(source.Length), out var decodedBytes))
            {
                memory.Return();
                return false;
            }

            memory.Advance(decodedBytes);
            return true;
        }
    }
}