using System;
using System.Buffers;
using System.Buffers.Binary;

namespace Thermite.Decoders.Mpeg
{
    internal static partial class MpegParser
    {
        public static bool TryReadMpegFrame(ReadOnlySequence<byte> sequence,
            out ReadOnlySequence<byte> frame,
            out int version, out int layer, out int bitrate,
            out int samplingRate, out int channelCount)
        {
            frame = ReadOnlySequence<byte>.Empty;
            version = default;
            layer = default;
            bitrate = default;
            samplingRate = default;
            channelCount = default;

            if (sequence.IsEmpty)
                return false;

            if (!TryFindFrameStart(sequence, out var start))
                return false;

            sequence = sequence.Slice(start);

            if (!TryReadFrameProperties(sequence.Slice(1, 1),
                out version, out layer))
                return false;

            if (!TryReadAudioProperties(sequence.Slice(2, 2),
                out bitrate, out samplingRate, out var hasPadding,
                out channelCount,
                version, layer))
                return false;

            var samples = SampleCountLookupTable[version, layer];
            var slotSize = SlotSizeLookupTable[layer];
            var audioSize = CalculateFrameSize(bitrate, samples, samplingRate, hasPadding, slotSize);

            if (sequence.Length < audioSize)
                return false;
            else if (sequence.Length > audioSize)
            {
                var offset = sequence.GetPosition(audioSize, start);
                if (!TryFindFrameStart(sequence.Slice(offset), out var end))
                    return false;

                frame = sequence.Slice(start, end);
            }
            else
                frame = sequence.Slice(start);

            version = VersionLookupTable[version];
            layer = LayerLookupTable[layer];

            return true;

            static int CalculateFrameSize(int bitrate, int samples, int sampleRate, bool hasPadding, int slotSize)
            {
                double bitsPerSample = samples / 8.0d;
                double frameSize = bitsPerSample * bitrate / sampleRate;

                return (int)frameSize + (hasPadding ? slotSize : 0);
            }
        }

        private static bool TryFindFrameStart(
            ReadOnlySequence<byte> sequence,
            out SequencePosition position)
        {
            position = default;

            while (true)
            {
                if (sequence.Length < 4)
                    return false;

                // Skip to the next potential frame start
                var frameSync = sequence.PositionOf<byte>(0xFF);
                if (frameSync == null)
                    return false;

                sequence = sequence.Slice(frameSync.Value);

                if (!TryReadFrameSync(sequence.Slice(0, 2)))
                {
                    // Skip the first byte so that the next PositionOf() does
                    // not return the same position.
                    sequence = sequence.Slice(1);
                    continue;
                }

                position = sequence.Start;
                return true;
            }

            static bool TryReadFrameSync(ReadOnlySequence<byte> sequence)
            {
                ushort frameSync = 0;

                if (sequence.IsSingleSegment)
                {
                    if (!BinaryPrimitives.TryReadUInt16BigEndian(
                        sequence.FirstSpan, out frameSync))
                        return false;
                }
                else
                {
                    Span<byte> tmp = stackalloc byte[2];
                    sequence.CopyTo(tmp);

                    if (!BinaryPrimitives.TryReadUInt16BigEndian(tmp,
                        out frameSync))
                        return false;
                }

                // All frame sync bits must be set
                return (frameSync & 0xFFF0) == 0xFFF0;
            }
        }

        private static bool TryReadAudioProperties(
            ReadOnlySequence<byte> sequence,
            out int bitrate, out int sampleRate, out bool hasPadding,
            out int channelCount, int version, int layer)
        {
            bitrate = default;
            sampleRate = default;
            hasPadding = default;
            channelCount = default;

            ushort audioProperties = 0;

            if (sequence.IsSingleSegment)
            {
                if (!BinaryPrimitives.TryReadUInt16BigEndian(
                    sequence.FirstSpan, out audioProperties))
                    return false;
            }
            else
            {
                Span<byte> tmp = stackalloc byte[2];
                sequence.CopyTo(tmp);

                if (!BinaryPrimitives.TryReadUInt16BigEndian(tmp,
                    out audioProperties))
                    return false;
            }

            var bitrateIndex = ((audioProperties >> 8) & 0b1111_00_0_0) >> 4;
            var sampleRateIndex = ((audioProperties >> 8) & 0b0000_11_0_0) >> 2;
            hasPadding = ((audioProperties >> 8) & 0b0000_00_1_0) != 0;
            var channelCountIndex = (audioProperties & 0b11_00_0_0_00) >> 6;

            if (bitrateIndex > MaximumBitrateIndex)
                return false;

            if (sampleRateIndex > MaximumSampleRateIndex)
                return false;

            if (channelCountIndex > ChannelCountLookupTable.Length)
                return false;

            bitrate = BitrateLookupTable[version, layer, bitrateIndex] * 1000;
            sampleRate = SampleRateLookupTable[version, sampleRateIndex];
            return true;
        }

        private static bool TryReadFrameProperties(
            ReadOnlySequence<byte> sequence, out int version,
            out int layer)
        {
            var properties = sequence.FirstSpan[0];

            version = (byte)(properties & 0b000_11_00_0) >> 3;
            layer = (byte)(properties & 0b000_00_11_0) >> 1;

            return VersionLookupTable[version] != 0
                && LayerLookupTable[version] != 0;
        }
    }
}
