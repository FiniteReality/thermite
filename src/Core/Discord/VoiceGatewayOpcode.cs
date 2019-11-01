namespace Thermite.Discord
{
    internal enum VoiceGatewayOpcode
    {
        Unknown = -1,
        Identify,
        SelectProtocol,
        Ready,
        Heartbeat,
        SessionDescription,
        Speaking,
        HeartbeatAck,
        Resume,
        Hello,
        Resumed
    }
}
