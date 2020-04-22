using System;
using System.Buffers;
using System.Diagnostics;

using static Thermite.Utilities.TextParsingUtilities;

namespace Thermite.Utilities
{
    /// <summary>
    /// Provides a set of methods for parsing UTF-8 encoded plaintext URLs.
    /// </summary>
    public static class UrlParsingUtilities
    {
        /// <summary>
        /// Attempts to read a key-value pair from a given query string. The
        /// initial <code>?</code> character should be stripped before calling
        /// this method.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method parses query strings whose key-value pairs are
        /// delimited by <code>&amp;</code> characters, and where the key and
        /// value are delimited by <code>=</code> characters.
        /// </para>
        /// <para>
        /// The <paramref name="sequence"/> parameter is updated to contain the
        /// next key-value pair, so that this method can be called as the
        /// condition of a while loop.
        /// </para>
        /// </remarks>
        /// <param name="sequence">
        /// The sequence to read a key-value pair from.
        /// </param>
        /// <param name="key">
        /// The part of <paramref name="sequence"/> which contains the
        /// potentially URL-encoded key.
        /// </param>
        /// <param name="value">
        /// The part of <paramref name="sequence"/> which contains the
        /// potentially URL-encoded value.
        /// </param>
        /// <returns>
        /// Returns <code>true</code> when a key-value pair was successfully
        /// read, and <code>false</code> otherwise.
        /// </returns>
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

            return TryReadTo(ref sequence, (byte)'=', out key)
                && TryReadTo(ref sequence, (byte)'&', out value);
        }

        /// <summary>
        /// Attempts to read a key-value pair from a given query string. The
        /// initial <code>?</code> character should be stripped before calling
        /// this method.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method parses query strings whose key-value pairs are
        /// delimited by <code>&amp;</code> characters, and where the key and
        /// value are delimited by <code>=</code> characters.
        /// </para>
        /// <para>
        /// The <paramref name="span"/> parameter is updated to contain the
        /// next key-value pair, so that this method can be called as the
        /// condition of a while loop.
        /// </para>
        /// </remarks>
        /// <param name="span">
        /// The <see cref="ReadOnlySpan{T}"/> to read a key-value pair from.
        /// </param>
        /// <param name="key">
        /// The part of <paramref name="span"/> which contains the
        /// potentially URL-encoded key.
        /// </param>
        /// <param name="value">
        /// The part of <paramref name="span"/> which contains the
        /// potentially URL-encoded value.
        /// </param>
        /// <returns>
        /// Returns <code>true</code> when a key-value pair was successfully
        /// read, and <code>false</code> otherwise.
        /// </returns>
        [DebuggerStepThrough]
        public static bool TryGetKeyValuePair(ref ReadOnlySpan<byte> span,
            out ReadOnlySpan<byte> key, out ReadOnlySpan<byte> value)
        {
            if (span.IsEmpty)
            {
                key = default;
                value = default;
                return false;
            }

            value = default;

            return TryReadTo(ref span, (byte)'=', out key)
                && TryReadTo(ref span, (byte)'&', out value);
        }

        /// <summary>
        /// Attempts to URL-decode a given <see cref="ReadOnlySpan{T}"/>.
        /// </summary>
        /// <param name="source">
        /// The source span containing URL-encoded data.
        /// </param>
        /// <param name="destination">
        /// The destination span to write plaintext data to.
        /// </param>
        /// <param name="decodedBytes">
        /// The number of bytes of decoded data.
        /// </param>
        /// <returns>
        /// Returns <code>true</code> when URL decoding was successful, and
        /// <code>false</code> otherwise.
        /// </returns>
        public static bool TryUrlDecode(ReadOnlySpan<byte> source,
            Span<byte> destination, out int decodedBytes)
        {
            decodedBytes = default;
            return destination.Length >= source.Length
                && source.TryCopyTo(destination)
                && TryUrlDecode(
                    destination.Slice(0, source.Length),
                    out decodedBytes);
        }

        /// <summary>
        /// Attempts to URL-decode a given <see cref="Span{T}"/> in-place.
        /// </summary>
        /// <param name="buffer">
        /// The span containing URL-encoded data.
        /// </param>
        /// <param name="decodedBytes">
        /// The number of bytes of decoded data.
        /// </param>
        /// <returns>
        /// Returns <code>true</code> when URL decoding was successful, and
        /// <code>false</code> otherwise.
        /// </returns>
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
