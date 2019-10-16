using System;
using System.Buffers.Text;
using System.Net;
using System.Text.Json;
using Thermite.Discord.Models;

namespace Thermite.Discord
{
    internal static partial class Payload
    {
        private const int MaximumUInt64Length = 20;
        private const int MaximumIPv4Length = 15;

        public static void WriteHeartbeat(Utf8JsonWriter writer, int nonce)
        {
            writer.WriteStartObject();
            writer.WriteNumber(OpcodePropertyName,
                (int)VoiceGatewayOpcode.Heartbeat);

            writer.WriteNumber(PayloadPropertyName, nonce);

            writer.WriteEndObject();
        }

        public static void WriteIdentify(Utf8JsonWriter writer,
            ulong userId, ulong guildId, ReadOnlySpan<byte> sessionId,
            ReadOnlySpan<byte> token)
        {
            writer.WriteStartObject();
            writer.WriteNumber(OpcodePropertyName,
                (int)VoiceGatewayOpcode.Identify);

            writer.WriteStartObject(PayloadPropertyName);

            Span<byte> tempBuffer = stackalloc byte[MaximumUInt64Length];

            _ = Utf8Formatter.TryFormat(guildId, tempBuffer, out var length);
            writer.WriteString(ServerIdPropertyName,
                tempBuffer.Slice(0, length));

            _ = Utf8Formatter.TryFormat(userId, tempBuffer, out length);
            writer.WriteString(UserIdPropertyName,
                tempBuffer.Slice(0, length));

            writer.WriteString(SessionIdPropertyName, sessionId);
            writer.WriteString(TokenPropertyName, token);

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        public static void WriteSelectProtocol(Utf8JsonWriter writer,
            IPEndPoint clientEndPoint, EncryptionModes bestMode)
        {
            writer.WriteStartObject();
            writer.WriteNumber(OpcodePropertyName,
                (int)VoiceGatewayOpcode.SelectProtocol);
            writer.WriteStartObject(PayloadPropertyName);

            writer.WriteString(ProtocolPropertyName, UdpValue);
            writer.WriteStartObject(DataPropertyName);

            Span<char> tempBuffer = stackalloc char[MaximumIPv4Length];
            _ = clientEndPoint.Address.TryFormat(tempBuffer, out int length);
            writer.WriteString(AddressPropertyName,
                tempBuffer.Slice(0, length));

            writer.WriteNumber(PortPropertyName, clientEndPoint.Port);
            writer.WriteString(ModePropertyName, bestMode switch
            {
                EncryptionModes.XSalsa20_Poly1305_Lite
                    => XSalsa20Poly1305LiteMode,
                _ => throw new InvalidOperationException("Unknown mode")
            });

            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        public static void WriteSpeaking(Utf8JsonWriter writer, bool speaking,
            int delay, uint ssrc)
        {
            writer.WriteStartObject();
            writer.WriteNumber(OpcodePropertyName,
                (int)VoiceGatewayOpcode.Speaking);
            writer.WriteStartObject(PayloadPropertyName);

            writer.WriteBoolean(SpeakingPropertyName, speaking);
            writer.WriteNumber(DelayPropertyName, delay);
            writer.WriteNumber(SsrcPropertyName, ssrc);

            writer.WriteEndObject();
            writer.WriteEndObject();
        }
    }
}