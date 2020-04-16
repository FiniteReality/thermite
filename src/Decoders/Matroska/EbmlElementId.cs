namespace Thermite.Decoders.Matroska
{
    internal enum EbmlElementId : uint
    {
        // Global elements
        Void = 0xEC,
        Crc32 = 0xBF,

        // EBML base elements
        Ebml = 0x1A45DFA3,
        EbmlVersion = 0x4286,
        EbmlReadVersion = 0x42F7,
        EbmlMaxIdLength = 0x42F2,
        EbmlMaxSizeLength = 0x42F3,
        EbmlDocType = 0x4282,
        EbmlDocTypeVersion = 0x4287,
        EbmlDocTypeReadVersion = 0x4285,

        // Matroska elements
        Segment = 0x18538067,
        SeekHead = 0x114D9B74,
        Info = 0x1549A966,
        Cluster = 0x1F43B675,
        Tracks = 0x1654AE6B,
        TrackEntry = 0xAE,
        Audio = 0xE1,
        Tags = 0x1254C367,

        SegmentUID = 0x73A4,

        Duration = 0x4489,
        TimestampScale = 0x2AD7B1,
        Title = 0x7BA9,
        MuxingApp = 0x4D80,
        WritingApp = 0x5741,
        Timestamp = 0xE7,

        TrackNumber = 0xD7,
        TrackUID = 0x73C5,
        TrackType = 0x83,
        FlagDefault = 0x88,
        FlagLacing = 0x9C,
        MaxBlockAdditionId = 0x55EE,
        Language = 0x22B59C,
        CodecId = 0x86,
        CodecPrivate = 0x63A2,
        CodecDecodeAll = 0xAA,
        CodecDelay = 0x56AA,
        SeekPreRoll = 0x56BB,

        SamplingFrequency = 0xB5,
        Channels = 0x9F,
        BitDepth = 0x6264,

        SimpleBlock = 0xA3,
        BlockGroup = 0xA0,
        Block = 0xA1
    }
}
