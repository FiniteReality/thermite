using System.Buffers;

namespace Thermite.Core.Decoders.Matroska
{
    internal struct MatroskaState
    {
        public MatroskaDocumentType DocumentType;
        public ulong TimestampScale;
        public ulong ClusterTimestamp;
        public ReadOnlySequence<byte> BlockData;
        public bool ProcessingTrack;
    }

    internal struct MatroskaTrack
    {
        public ulong TrackNumber;
        public MatroskaCodec CodecId;
        public IMemoryOwner<byte> CodecData;
        public int CodecDataLength;
        public bool CodecDecodesAll;
        public double SampleRate;
        public ulong ChannelCount;
        public ulong BitDepth;
        public ulong SeekPreRoll;
        public bool IsDefault;
        public bool IsAudio;
        public bool IsLaced;
    }

    internal enum MatroskaDocumentType
    {
        Matroska,
        WebM
    }

    internal enum MatroskaCodec
    {
        Opus,
        Vorbis
    }
}
