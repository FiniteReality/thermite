using System;
using System.Buffers;
using System.Numerics;
using System.Text;

using static Thermite.Internal.TextParsingUtilities;

namespace Thermite.Decoders.Matroska
{
    internal enum EbmlHandleStatus
    {
        Success,
        NewTrack,
        NewCluster,
        NewBlock,

        MissingData,
        UnknownElementId,

        UnsupportedFile,

        NoMoreData
    }

    internal static partial class EbmlParser
    {
        private const int MaxSupportedMatroskaVersion = 3;
        private const int MaxSupportedWebmVersion = 2;

        private static readonly byte[] MatroskaDocType =
            Encoding.UTF8.GetBytes("matroska");
        private static readonly byte[] WebmDocType =
            Encoding.UTF8.GetBytes("webm");

        private static readonly byte[] OpusCodecType =
            Encoding.UTF8.GetBytes("A_OPUS");
        private static readonly byte[] MpegLayer3CodecType =
            Encoding.UTF8.GetBytes("A_MPEG/L3");
        private static readonly byte[] MpegLayer2CodecType =
            Encoding.UTF8.GetBytes("A_MPEG/L2");
        private static readonly byte[] MpegLayer1CodecType =
            Encoding.UTF8.GetBytes("A_MPEG/L1");

        public static EbmlHandleStatus TryHandleEbmlElement(
            ref MatroskaState state, ref MatroskaTrack currentAudioTrack,
            EbmlElementId elementId, ulong length,
            ref ReadOnlySequence<byte> buffer)
        {
            switch (elementId)
            {
                // Global elements
                case EbmlElementId.Void:
                case EbmlElementId.Crc32: // CRC32 ignored for now...
                    if (buffer.Length < (long)length)
                        return EbmlHandleStatus.MissingData;

                    buffer = buffer.Slice((int)length);
                    return EbmlHandleStatus.Success;

                // Container elements
                case EbmlElementId.Ebml:
                case EbmlElementId.Segment:
                case EbmlElementId.Info:
                case EbmlElementId.Tracks:
                case EbmlElementId.Audio:
                case EbmlElementId.BlockGroup:
                    return EbmlHandleStatus.Success;
                case EbmlElementId.Cluster:
                {
                    if (state.ProcessingTrack)
                    {
                        state.ProcessingTrack = false;
                        return EbmlHandleStatus.NewTrack;
                    }

                    return EbmlHandleStatus.Success;
                }
                case EbmlElementId.TrackEntry:
                {
                    if (state.ProcessingTrack)
                        return EbmlHandleStatus.NewTrack;
                    else
                        state.ProcessingTrack = true;

                    return EbmlHandleStatus.Success;
                }

                case EbmlElementId.Block:
                case EbmlElementId.SimpleBlock:
                {
                    if (buffer.Length < (long)length)
                        return EbmlHandleStatus.MissingData;

                    state.BlockData = buffer.Slice(0, (int)length);
                    buffer = buffer.Slice((int)length);
                    return EbmlHandleStatus.NewBlock;
                }

                // EBML Value elements
                case EbmlElementId.EbmlReadVersion:
                {
                    if (buffer.Length < (long)length)
                        return EbmlHandleStatus.MissingData;

                    if (!TryReadEbmlUnsigned(buffer.Slice(0, (int)length),
                        out var version))
                        return EbmlHandleStatus.MissingData;

                    if (version > 1)
                        return EbmlHandleStatus.UnsupportedFile;

                    buffer = buffer.Slice((int)length);
                    return EbmlHandleStatus.Success;
                }
                case EbmlElementId.EbmlMaxIdLength:
                {
                    if (buffer.Length < (long)length)
                        return EbmlHandleStatus.MissingData;

                    if (!TryReadEbmlUnsigned(buffer.Slice(0, (int)length),
                        out var maxLength))
                        return EbmlHandleStatus.MissingData;

                    if (maxLength > 4)
                        return EbmlHandleStatus.UnsupportedFile;

                    buffer = buffer.Slice((int)length);
                    return EbmlHandleStatus.Success;
                }
                case EbmlElementId.EbmlMaxSizeLength:
                {
                    if (buffer.Length < (long)length)
                        return EbmlHandleStatus.MissingData;

                    if (!TryReadEbmlUnsigned(buffer.Slice(0, (int)length),
                        out var maxLength))
                        return EbmlHandleStatus.MissingData;

                    if (maxLength > 8)
                        return EbmlHandleStatus.UnsupportedFile;

                    buffer = buffer.Slice((int)length);
                    return EbmlHandleStatus.Success;
                }
                case EbmlElementId.EbmlDocType:
                {
                    if (buffer.Length < (long)length)
                        return EbmlHandleStatus.MissingData;

                    var doctype = buffer.Slice(0, (int)length);

                    if (SequenceEqual(doctype, MatroskaDocType))
                        state.DocumentType = MatroskaDocumentType.Matroska;
                    else if (SequenceEqual(doctype, WebmDocType))
                        state.DocumentType = MatroskaDocumentType.WebM;
                    else
                        return EbmlHandleStatus.UnsupportedFile;

                    buffer = buffer.Slice((int)length);
                    return EbmlHandleStatus.Success;
                }
                case EbmlElementId.EbmlDocTypeReadVersion:
                {
                    if (buffer.Length < (long)length)
                        return EbmlHandleStatus.MissingData;

                    if (!TryReadEbmlUnsigned(buffer.Slice(0, (int)length),
                        out var version))
                        return EbmlHandleStatus.MissingData;

                    if (state.DocumentType == MatroskaDocumentType.Matroska
                        && version > MaxSupportedMatroskaVersion)
                        return EbmlHandleStatus.UnsupportedFile;

                    else if (state.DocumentType == MatroskaDocumentType.WebM
                        && version > MaxSupportedWebmVersion)
                        return EbmlHandleStatus.UnsupportedFile;

                    buffer = buffer.Slice((int)length);
                    return EbmlHandleStatus.Success;
                }

                // Matroska elements
                case EbmlElementId.Timestamp:
                {
                    if (buffer.Length < (long)length)
                        return EbmlHandleStatus.MissingData;

                    if (!TryReadEbmlUnsigned(buffer.Slice(0, (int)length),
                        out var timestamp))
                        return EbmlHandleStatus.MissingData;

                    state.ClusterTimestamp = timestamp;

                    buffer = buffer.Slice((int)length);
                    return EbmlHandleStatus.NewCluster;
                }

                case EbmlElementId.TimestampScale:
                {
                    if (buffer.Length < (long)length)
                        return EbmlHandleStatus.MissingData;

                    if (!TryReadEbmlUnsigned(buffer.Slice(0, (int)length),
                        out var scale))
                        return EbmlHandleStatus.MissingData;

                    state.TimestampScale = scale;

                    buffer = buffer.Slice((int)length);
                    return EbmlHandleStatus.Success;
                }
                case EbmlElementId.TrackNumber:
                {
                    if (buffer.Length < (long)length)
                        return EbmlHandleStatus.MissingData;

                    if (!TryReadEbmlUnsigned(buffer.Slice(0, (int)length),
                        out var trackNumber))
                        return EbmlHandleStatus.MissingData;

                    currentAudioTrack.TrackNumber = trackNumber;

                    buffer = buffer.Slice((int)length);
                    return EbmlHandleStatus.Success;
                }
                case EbmlElementId.TrackType:
                {
                    if (buffer.Length < (long)length)
                        return EbmlHandleStatus.MissingData;

                    if (!TryReadEbmlUnsigned(buffer.Slice(0, (int)length),
                        out var trackType))
                        return EbmlHandleStatus.MissingData;

                    if (trackType == 2) // audio
                        currentAudioTrack.IsAudio = true;

                    buffer = buffer.Slice((int)length);
                    return EbmlHandleStatus.Success;
                }
                case EbmlElementId.FlagDefault:
                {
                    if (buffer.Length < (long)length)
                        return EbmlHandleStatus.MissingData;

                    if (!TryReadEbmlUnsigned(buffer.Slice(0, (int)length),
                        out var isDefault))
                        return EbmlHandleStatus.MissingData;

                    if (isDefault != 0)
                        currentAudioTrack.IsDefault = true;

                    buffer = buffer.Slice((int)length);
                    return EbmlHandleStatus.Success;
                }
                case EbmlElementId.FlagLacing:
                {
                    if (buffer.Length < (long)length)
                        return EbmlHandleStatus.MissingData;

                    if (!TryReadEbmlUnsigned(buffer.Slice(0, (int)length),
                        out var isLaced))
                        return EbmlHandleStatus.MissingData;

                    if (isLaced != 0)
                        currentAudioTrack.IsLaced = true;

                    buffer = buffer.Slice((int)length);
                    return EbmlHandleStatus.Success;
                }
                case EbmlElementId.MaxBlockAdditionId:
                {
                    if (buffer.Length < (long)length)
                        return EbmlHandleStatus.MissingData;

                    if (!TryReadEbmlUnsigned(buffer.Slice(0, (int)length),
                        out var blockAdditions))
                        return EbmlHandleStatus.MissingData;

                    if (blockAdditions > 0)
                        return EbmlHandleStatus.UnsupportedFile;

                    buffer = buffer.Slice((int)length);
                    return EbmlHandleStatus.Success;
                }
                case EbmlElementId.CodecId:
                {
                    if (buffer.Length < (long)length)
                        return EbmlHandleStatus.MissingData;

                    var codec = buffer.Slice(0, (int)length);
                    if (SequenceEqual(codec, OpusCodecType))
                        currentAudioTrack.CodecId = MatroskaCodec.Opus;
                    else if (SequenceEqual(codec, MpegLayer3CodecType))
                        currentAudioTrack.CodecId = MatroskaCodec.MpegLayer3;
                    else if (SequenceEqual(codec, MpegLayer2CodecType))
                        currentAudioTrack.CodecId = MatroskaCodec.MpegLayer2;
                    else if (SequenceEqual(codec, MpegLayer1CodecType))
                        currentAudioTrack.CodecId = MatroskaCodec.MpegLayer1;
                    else
                        return EbmlHandleStatus.UnsupportedFile;

                    buffer = buffer.Slice((int)length);
                    return EbmlHandleStatus.Success;
                }
                case EbmlElementId.CodecPrivate:
                {
                    if (buffer.Length < (long)length)
                        return EbmlHandleStatus.MissingData;

                    var data = buffer.Slice(0, (int)length);
                    var copy = MemoryPool<byte>.Shared.Rent((int)length);

                    data.CopyTo(copy.Memory.Span);

                    currentAudioTrack.CodecData = copy;
                    currentAudioTrack.CodecDataLength = (int)length;

                    buffer = buffer.Slice((int)length);
                    return EbmlHandleStatus.Success;
                }
                case EbmlElementId.CodecDecodeAll:
                {
                    if (buffer.Length < (long)length)
                        return EbmlHandleStatus.MissingData;

                    if (!TryReadEbmlUnsigned(buffer.Slice(0, (int)length),
                        out var decodeAll))
                        return EbmlHandleStatus.MissingData;

                    if (decodeAll != 0)
                        currentAudioTrack.CodecDecodesAll = true;

                    buffer = buffer.Slice((int)length);
                    return EbmlHandleStatus.Success;
                }
                case EbmlElementId.SeekPreRoll:
                {
                    if (buffer.Length < (long)length)
                        return EbmlHandleStatus.MissingData;

                    if (!TryReadEbmlUnsigned(buffer.Slice(0, (int)length),
                        out var preroll))
                        return EbmlHandleStatus.MissingData;

                    currentAudioTrack.SeekPreRoll = preroll;

                    buffer = buffer.Slice((int)length);
                    return EbmlHandleStatus.Success;
                }
                case EbmlElementId.SamplingFrequency:
                {
                    if (buffer.Length < (long)length)
                        return EbmlHandleStatus.MissingData;

                    if (!TryReadEbmlFloat(buffer.Slice(0, (int)length),
                        out var sampleRate))
                        return EbmlHandleStatus.MissingData;

                    currentAudioTrack.SampleRate = sampleRate;

                    buffer = buffer.Slice((int)length);
                    return EbmlHandleStatus.Success;
                }
                case EbmlElementId.Channels:
                {
                    if (buffer.Length < (long)length)
                        return EbmlHandleStatus.MissingData;

                    if (!TryReadEbmlUnsigned(buffer.Slice(0, (int)length),
                        out var channels))
                        return EbmlHandleStatus.MissingData;

                    currentAudioTrack.ChannelCount = channels;

                    buffer = buffer.Slice((int)length);
                    return EbmlHandleStatus.Success;
                }
                case EbmlElementId.BitDepth:
                {
                    if (buffer.Length < (long)length)
                        return EbmlHandleStatus.MissingData;

                    if (!TryReadEbmlUnsigned(buffer.Slice(0, (int)length),
                        out var bitDepth))
                        return EbmlHandleStatus.MissingData;

                    currentAudioTrack.BitDepth = bitDepth;

                    buffer = buffer.Slice((int)length);
                    return EbmlHandleStatus.Success;
                }

                // Default case
                default:
                {
                    if (buffer.Length < (long)length)
                        return EbmlHandleStatus.MissingData;

                    buffer = buffer.Slice((int)length);
                    return EbmlHandleStatus.UnknownElementId;
                }
            }
        }
    }
}
