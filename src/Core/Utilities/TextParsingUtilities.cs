using System;
using System.Buffers;
using System.Diagnostics;

namespace Thermite.Utilities
{
    /// <summary>
    /// Provides a set of methods for parsing structured UTF-8 plaintext.
    /// </summary>
    public static class TextParsingUtilities
    {
        /// <summary>
        /// Attempts to read a given <see cref="ReadOnlySequence{T}"/> until
        /// the specified separator is encountered, or until the end of the
        /// sequence.
        /// </summary>
        /// <remarks>
        /// The <paramref name="sequence"/> parameter is updated to point at
        /// the first byte after <paramref name="separator"/>, so that this
        /// method can be called as the condition of a while loop.
        /// </remarks>
        /// <param name="sequence">
        /// The <see cref="ReadOnlySequence{T}"/> to read from.
        /// </param>
        /// <param name="separator">
        /// The separator to look for in <paramref name="sequence"/>.
        /// </param>
        /// <param name="skipped">
        /// The bytes skipped to encounter <paramref name="separator"/>, not
        /// including the separator itself.
        /// </param>
        /// <returns>
        /// Returns <code>true</code> when <paramref name="separator"/> or the
        /// end of the sequence was encountered, and <code>false</code>
        /// otherwise.
        /// </returns>
        [DebuggerStepThrough]
        public static bool TryReadTo(ref ReadOnlySequence<byte> sequence,
            byte separator, out ReadOnlySequence<byte> skipped)
        {
            if (sequence.IsEmpty)
            {
                skipped = default;
                return false;
            }

            var position = sequence.PositionOf(separator);

            if (position == null)
            {
                skipped = sequence;
                sequence = ReadOnlySequence<byte>.Empty;
                return true;
            }

            skipped = sequence.Slice(sequence.Start, position.Value);
            sequence = sequence.Slice(sequence.GetPosition(1, position.Value));
            return true;
        }


        /// <summary>
        /// Attempts to read a given <see cref="ReadOnlySpan{T}"/> until the
        /// specified separator is encountered, or until the end of the span.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <paramref name="span"/> parameter is updated to point at the
        /// first byte after <paramref name="separator"/>, so that this method
        /// can be called as the condition of a while loop.
        /// </para>
        /// </remarks>
        /// <param name="span">
        /// The <see cref="ReadOnlySpan{T}"/> to read from.
        /// </param>
        /// <param name="separator">
        /// The separator to look for in <paramref name="span"/>.
        /// </param>
        /// <param name="skipped">
        /// The bytes skipped to encounter <paramref name="separator"/>, not
        /// including the separator itself.
        /// </param>
        /// <returns>
        /// Returns <code>true</code> when <paramref name="separator"/> or the
        /// end of the span was encountered, and <code>false</code> otherwise.
        /// </returns>
        [DebuggerStepThrough]
        public static bool TryReadTo(ref ReadOnlySpan<byte> span,
            byte separator, out ReadOnlySpan<byte> skipped)
        {
            if (span.IsEmpty)
            {
                skipped = default;
                return false;
            }

            var position = span.IndexOf(separator);

            if (position < 0)
            {
                skipped = span;
                span = ReadOnlySpan<byte>.Empty;
                return true;
            }

            skipped = span.Slice(0, position);
            span = span.Slice(position + 1);
            return true;
        }

        /// <summary>
        /// Checks for equality between a <see cref="ReadOnlySequence{T}"/> and
        /// a <see cref="ReadOnlySpan{T}"/>
        /// </summary>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <returns></returns>
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
    }
}
