namespace Thermite.Discord.Models
{
    internal ref struct VoiceGatewayPayload
    {
        public VoiceGatewayOpcode Opcode;

        public VoiceGatewayHello Hello;

        public VoiceGatewayReady Ready;

        public int Nonce;
    }
}
