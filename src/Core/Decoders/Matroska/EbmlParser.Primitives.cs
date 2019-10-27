using System;
using System.Buffers;
using System.Numerics;

using static System.Buffers.Binary.BinaryPrimitives;

namespace Thermite.Core.Decoders.Matroska
{
    internal static partial class EbmlParser
    {
        internal static bool TryReadEbmlSigned(
            ReadOnlySequence<byte> sequence,
            out long value)
        {
            if (sequence.IsSingleSegment && sequence.Length == 8)
                return TryReadInt64BigEndian(sequence.FirstSpan, out value);

            Span<byte> buffer = stackalloc byte[8];
            sequence.CopyTo(buffer.Slice(8 - (int)sequence.Length));

            return TryReadInt64BigEndian(buffer, out value);
        }

        internal static bool TryReadEbmlUnsigned(
            ReadOnlySequence<byte> sequence,
            out ulong value)
        {
            if (sequence.IsSingleSegment && sequence.Length == 8)
                return TryReadUInt64BigEndian(sequence.FirstSpan, out value);

            Span<byte> buffer = stackalloc byte[8];
            sequence.CopyTo(buffer.Slice(8 - (int)sequence.Length));

            return TryReadUInt64BigEndian(buffer, out value);
        }

        internal static bool TryReadEbmlFloat(
            ReadOnlySequence<byte> sequence,
            out double value)
        {
            value = default;

            if (sequence.Length == sizeof(float))
            {
                if (TryReadSingle(sequence, out value))
                    return true;
            }
            else if (sequence.Length == sizeof(double))
            {
                if (TryReadDouble(sequence, out value))
                    return true;
            }

            return false;

            static bool TryReadSingle(ReadOnlySequence<byte> sequence,
                out double value)
            {
                value = default;
                int valueInt;

                if (sequence.IsSingleSegment)
                {
                    if (!TryReadInt32BigEndian(sequence.FirstSpan,
                        out valueInt))
                        return false;

                    value = BitConverter.Int32BitsToSingle(valueInt);
                    return true;
                }

                Span<byte> buffer = stackalloc byte[sizeof(float)];
                sequence.CopyTo(buffer);

                if (!TryReadInt32BigEndian(sequence.FirstSpan,
                    out valueInt))
                    return false;

                value = BitConverter.Int32BitsToSingle(valueInt);
                return true;
            }

            static bool TryReadDouble(ReadOnlySequence<byte> sequence,
                out double value)
            {
                value = default;
                long valueInt;

                if (sequence.IsSingleSegment)
                {
                    if (!TryReadInt64BigEndian(sequence.FirstSpan,
                        out valueInt))
                        return false;

                    value = BitConverter.Int64BitsToDouble(valueInt);
                    return true;
                }

                Span<byte> buffer = stackalloc byte[sizeof(float)];
                sequence.CopyTo(buffer);

                if (!TryReadInt64BigEndian(sequence.FirstSpan,
                    out valueInt))
                    return false;

                value = BitConverter.Int64BitsToDouble(valueInt);
                return true;
            }
        }

        internal static bool TryReadEbmlEncodedInt(
                ref ReadOnlySpan<byte> input,
                out ulong value)
        {
            value = default;

            if (input.IsEmpty)
                return false;

            var length = BitOperations.LeadingZeroCount(
                (input[0] & 0xFFU) << 24) + 1;

            if (length > 8)
                return false;

            if (input.Length < length)
                return false;

            value = input[0];
            value &= ~(1u << (8 - length));

            for (int i = 1; i < length; i++)
            {
                value = (value << 8) | (input[i] & 0xFFU);
            }

            input = input.Slice(length);
            return true;
        }

        internal static bool TryReadEbmlElement(
                ReadOnlySequence<byte> sequence,
                out uint elementId,
                out ulong elementLength,
                out ReadOnlySequence<byte> data)
        {
            elementId = 0;
            elementLength = default;
            data = sequence;

            if (sequence.IsEmpty)
                return false;

            if (!TryReadElementId(data, out elementId,
                out var elementClass))
                return false;

            data = data.Slice(elementClass);

            if (data.IsEmpty)
                return false;

            if (!TryReadSize(data, out elementLength,
                out var elementLengthSize))
                return false;

            data = data.Slice(elementLengthSize);
            return true;

            static bool TryReadSize(
                ReadOnlySequence<byte> sequence,
                out ulong elementLength,
                out int elementLengthSize)
            {
                elementLength = 0;
                elementLengthSize = BitOperations.LeadingZeroCount(
                    (sequence.FirstSpan[0] & 0xFFU) << 24) + 1;

                if (elementLengthSize > 8)
                    return false;

                if (sequence.FirstSpan.Length > elementLengthSize)
                {
                    elementLength = sequence.FirstSpan[0];
                    elementLength &= ~(1u << (8 - elementLengthSize));

                    for (int i = 1; i < elementLengthSize; i++)
                    {
                        elementLength = (elementLength << 8) |
                            (sequence.FirstSpan[i] & 0xFFU);
                    }
                }
                else
                {
                    Span<byte> buffer = stackalloc byte[elementLengthSize];
                    sequence.Slice(0, elementLengthSize).CopyTo(buffer);

                    elementLength = buffer[0];
                    elementLength &= ~(1u << (8 - elementLengthSize));

                    for (int i = 1; i < elementLengthSize; i++)
                    {
                        elementLength = (elementLength << 8) |
                            (buffer[i] & 0xFFU);
                    }
                }

                return true;
            }

            static bool TryReadElementId(
                ReadOnlySequence<byte> sequence,
                out uint elementId,
                out int elementClass)
            {
                elementId = 0;
                elementClass = BitOperations.LeadingZeroCount(
                    (sequence.FirstSpan[0] & 0xF0U) << 24) + 1;

                if (elementClass > 4)
                    return false;

                if (sequence.FirstSpan.Length > elementClass)
                {
                    for (int i = 0; i < elementClass; i++)
                    {
                        elementId = (elementId << 8) |
                            (sequence.FirstSpan[i] & 0xFFU);
                    }
                }
                else
                {
                    Span<byte> buffer = stackalloc byte[elementClass];
                    sequence.Slice(0, elementClass).CopyTo(buffer);

                    for (int i = 0; i < elementClass; i++)
                    {
                        elementId = (elementId << 8) |
                            (buffer[i] & 0xFFU);
                    }
                }

                return true;
            }
        }
    }
}