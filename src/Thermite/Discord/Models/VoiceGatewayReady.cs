using System;
using System.Buffers;
using System.Text.Json.Serialization;

namespace Thermite.Discord.Models
{
    internal struct VoiceGatewayReady
    {
        [JsonPropertyName("ssrc")]
        public int Ssrc { get; set; }

        [JsonPropertyName("ip")]
        public ReadOnlySequence<byte> Ip { get; set; } // :(

        [JsonPropertyName("port")]
        public int Port { get; set; }

        [JsonPropertyName("modes")]
        public ReadOnlySequence<byte> Modes { get; set; } // :(((
    }
}