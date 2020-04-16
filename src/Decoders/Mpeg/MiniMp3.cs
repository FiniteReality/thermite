using System.Runtime.InteropServices;
using Thermite.Utilities;

namespace Thermite.Natives
{
    internal partial struct mp3dec_frame_info_t
    {
        public int frame_bytes;

        public int frame_offset;

        public int channels;

        public int hz;

        public int layer;

        public int bitrate_kbps;
    }

    internal unsafe partial struct mp3dec_t
    {
        [NativeTypeName("float [2][288]")]
        public fixed float mdct_overlap[2 * 288];

        [NativeTypeName("float [960]")]
        public fixed float qmf_state[960];

        public int reserv;

        public int free_format_bytes;

        [NativeTypeName("unsigned char [4]")]
        public fixed byte header[4];

        [NativeTypeName("unsigned char [511]")]
        public fixed byte reserv_buf[511];
    }

    internal static unsafe partial class MiniMp3
    {
        private const string LibraryPath = "minimp3";

        public const int MINIMP3_MAX_SAMPLES_PER_FRAME = 1152 * 2;

        [DllImport(LibraryPath, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mp3dec_init", ExactSpelling = true)]
        public static extern void mp3dec_init([NativeTypeName("mp3dec_t *")] mp3dec_t* dec);

        [DllImport(LibraryPath, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mp3dec_decode_frame", ExactSpelling = true)]
        public static extern int mp3dec_decode_frame([NativeTypeName("mp3dec_t *")] mp3dec_t* dec, [NativeTypeName("const uint8_t *")] byte* mp3, int mp3_bytes, [NativeTypeName("mp3d_sample_t *")] short* pcm, [NativeTypeName("mp3dec_frame_info_t *")] mp3dec_frame_info_t* info);
    }
}
