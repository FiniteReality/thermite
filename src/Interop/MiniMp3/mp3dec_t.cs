using System.Runtime.InteropServices;
using Thermite.Utilities;

namespace Thermite.Interop
{
    public unsafe partial struct mp3dec_t
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
}
