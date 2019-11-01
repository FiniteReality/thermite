using System.Text.Json.Serialization;

namespace Thermite.Discord.Models
{
    internal struct VoiceGatewayPayload<T>
    {
        [JsonPropertyName("op")]
        public VoiceGatewayOpcode Opcode { get; set; }

        [JsonPropertyName("d")]
        public T Payload { get; set; }
    }
}
