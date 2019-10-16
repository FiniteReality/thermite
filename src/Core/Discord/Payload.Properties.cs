using System.Text;

namespace Thermite.Discord
{
    internal static partial class Payload
    {
        // packet
        private static readonly byte[] OpcodePropertyName =
            Encoding.UTF8.GetBytes("op");
        private static readonly byte[] PayloadPropertyName =
            Encoding.UTF8.GetBytes("d");

        // hello
        private static readonly byte[] HeartbeatIntervalPropertyName =
            Encoding.UTF8.GetBytes("heartbeat_interval");

        // identify
        private static readonly byte[] ServerIdPropertyName =
            Encoding.UTF8.GetBytes("server_id");
        private static readonly byte[] UserIdPropertyName =
            Encoding.UTF8.GetBytes("user_id");
        private static readonly byte[] SessionIdPropertyName =
            Encoding.UTF8.GetBytes("session_id");
        private static readonly byte[] TokenPropertyName =
            Encoding.UTF8.GetBytes("token");

        // ready
        private static readonly byte[] SsrcPropertyName =
            Encoding.UTF8.GetBytes("ssrc");
        private static readonly byte[] IpPropertyName =
            Encoding.UTF8.GetBytes("ip");
        private static readonly byte[] PortPropertyName =
            Encoding.UTF8.GetBytes("port");
        private static readonly byte[] ModesPropertyName =
            Encoding.UTF8.GetBytes("modes");

        // select protocol
        private static readonly byte[] ProtocolPropertyName =
            Encoding.UTF8.GetBytes("protocol");
        private static readonly byte[] UdpValue =
            Encoding.UTF8.GetBytes("udp");
        private static readonly byte[] DataPropertyName =
            Encoding.UTF8.GetBytes("data");
        private static readonly byte[] AddressPropertyName =
            Encoding.UTF8.GetBytes("address");
        private static readonly byte[] ModePropertyName =
            Encoding.UTF8.GetBytes("mode");

        // session description
        private static readonly byte[] SecretKeyPropertyName =
            Encoding.UTF8.GetBytes("secret_key");

        // encryption modes
        private static readonly byte[] PlainMode =
            Encoding.UTF8.GetBytes("plain");
        private static readonly byte[] XSalsa20Poly1305Mode =
            Encoding.UTF8.GetBytes("xsalsa20_poly1305");
        private static readonly byte[] XSalsa20Poly1305SuffixMode =
            Encoding.UTF8.GetBytes("xsalsa20_poly1305_suffix");
        private static readonly byte[] XSalsa20Poly1305LiteMode =
            Encoding.UTF8.GetBytes("xsalsa20_poly1305_lite");

        // speaking
        public static readonly byte[] SpeakingPropertyName =
            Encoding.UTF8.GetBytes("speaking");
        public static readonly byte[] DelayPropertyName =
            Encoding.UTF8.GetBytes("delay");
    }
}