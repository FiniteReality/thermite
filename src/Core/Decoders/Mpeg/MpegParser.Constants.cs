using System;
using System.Buffers;
using System.Buffers.Binary;

namespace Thermite.Decoders.Mpeg
{
    internal static partial class MpegParser
    {
        // Lookup tables taken from
        // https://hydrogenaud.io/index.php?topic=85125.msg747716#msg747716

        private static readonly int[] VersionLookupTable
            = new[]
            {
                25, // MPEG 2.5
                0, // Reserved
                2, // MPEG 2
                1, // MPEG 1
            };

        private static readonly int[] LayerLookupTable
            = new[]
            {
                0, // Reserved
                3, // Layer III
                2, // Layer II
                1, // Layer I
            };

        private static readonly int[,,] BitrateLookupTable
            = new[,,]
            {
                { // Version 2.5
                    { 0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 0 }, // Reserved
                    { 0,   8,  16,  24,  32,  40,  48,  56,  64,  80,  96, 112, 128, 144, 160, 0 }, // Layer 3
                    { 0,   8,  16,  24,  32,  40,  48,  56,  64,  80,  96, 112, 128, 144, 160, 0 }, // Layer 2
                    { 0,  32,  48,  56,  64,  80,  96, 112, 128, 144, 160, 176, 192, 224, 256, 0 }  // Layer 1
                },
                { // Reserved
                    { 0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 0 }, // Invalid
                    { 0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 0 }, // Invalid
                    { 0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 0 }, // Invalid
                    { 0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 0 }  // Invalid
                },
                { // Version 2
                    { 0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 0 }, // Reserved
                    { 0,   8,  16,  24,  32,  40,  48,  56,  64,  80,  96, 112, 128, 144, 160, 0 }, // Layer 3
                    { 0,   8,  16,  24,  32,  40,  48,  56,  64,  80,  96, 112, 128, 144, 160, 0 }, // Layer 2
                    { 0,  32,  48,  56,  64,  80,  96, 112, 128, 144, 160, 176, 192, 224, 256, 0 }  // Layer 1
                },
                { // Version 1
                    { 0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 0 }, // Reserved
                    { 0,  32,  40,  48,  56,  64,  80,  96, 112, 128, 160, 192, 224, 256, 320, 0 }, // Layer 3
                    { 0,  32,  48,  56,  64,  80,  96, 112, 128, 160, 192, 224, 256, 320, 384, 0 }, // Layer 2
                    { 0,  32,  64,  96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448, 0 }, // Layer 1
                }
            };
        private static readonly int MaximumBitrateIndex
            = BitrateLookupTable.GetLength(2);

        private static readonly int[,] SampleRateLookupTable
            = new[,]
            {
                { 11025, 12000,  8000, 0 }, // MPEG 2.5
                {     0,     0,     0, 0 }, // Reserved
                { 22050, 24000, 16000, 0 }, // MPEG 2
                { 44100, 48000, 32000, 0 }  // MPEG 1
            };
        private static readonly int MaximumSampleRateIndex
            = BitrateLookupTable.GetLength(1);

        private static readonly int[,] SampleCountLookupTable
            = new[,]
            {
            //    Rsvd     3     2     1  < Layer  v Version
                {    0,  576, 1152,  384 }, //       2.5
                {    0,    0,    0,    0 }, //       Reserved
                {    0,  576, 1152,  384 }, //       2
                {    0, 1152, 1152,  384 }  //       1
            };
        private static readonly int MaximumSampleCountIndex
            = BitrateLookupTable.GetLength(1);

        private static readonly int[] SlotSizeLookupTable
            = new[]
            {
                0, // Reserved
                1, // Layer 3
                1, // Layer 2
                4 // Layer 1
            };

        private static readonly int[] ChannelCountLookupTable
            = new int[]
            {
                2, // Stereo
                2, // Joint Stereo (aka stereo)
                2, // Dual channel (2 mono channels)
                1, // Single channel (mono)
            };
    }
}
