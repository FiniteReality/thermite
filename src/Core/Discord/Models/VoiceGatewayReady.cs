using System;
using System.Runtime.InteropServices;

namespace Thermite.Discord.Models
{
    internal unsafe struct VoiceGatewayReady
    {
        public uint Ssrc;

        private fixed byte _ip[15];
        public Span<byte> RawIp
            => MemoryMarshal.CreateSpan(ref _ip[0], 15);
        public Span<byte> SlicedIp
            => RawIp.TrimEnd((byte)0);

        public int Port;

        public EncryptionModes Modes;
    }

    [Flags]
    internal enum EncryptionModes : uint
    {
        Unknown = 0,
        XSalsa20_Poly1305 = 1 << 0,
        XSalsa20_Poly1305_Suffix = 1 << 1,
        XSalsa20_Poly1305_Lite = 1 << 2
    }
}