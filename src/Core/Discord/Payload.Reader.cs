using System;
using System.Buffers;
using System.Text.Json;
using Thermite.Discord.Models;

namespace Thermite.Discord
{
    internal static partial class Payload
    {
        public static bool TryReadOpcode(ref Utf8JsonReader reader,
            out VoiceGatewayOpcode opcode)
        {
            opcode = default;

            if (!reader.TryReadToken(JsonTokenType.StartObject))
                return false;

            if (!reader.TryReadToken(JsonTokenType.PropertyName)
                || !reader.ValueTextEquals(OpcodePropertyName))
                return false;

            if (!reader.TryReadToken(JsonTokenType.Number)
                || !reader.TryGetInt32(out int rawOpcode))
                return false;

            if (!reader.TryReadToken(JsonTokenType.PropertyName)
                || !reader.ValueTextEquals(PayloadPropertyName))
                return false;

            opcode = (VoiceGatewayOpcode)rawOpcode;
            return true;
        }

        public static bool TryReadHeartbeatAck(ref Utf8JsonReader reader,
            out int nonce)
        {
            nonce = default;

            return reader.TryReadToken(JsonTokenType.Number)
                && reader.TryGetInt32(out nonce);
        }

        public static bool TryReadHello(ref Utf8JsonReader reader,
            out VoiceGatewayHello hello)
        {
            hello = default;

            if (!reader.TryReadToken(JsonTokenType.StartObject))
                return false;

            int read = 0;
            while (reader.TryReadToken(JsonTokenType.PropertyName))
            {
                if (reader.ValueTextEquals(HeartbeatIntervalPropertyName))
                {
                    if (!reader.TryReadToken(JsonTokenType.Number)
                        || !reader.TryGetDouble(out var interval))
                        return false;

                    hello.HeartbeatInterval = TimeSpan.FromMilliseconds(
                        interval);
                    read++;
                }
                else
                {
                    if (!reader.TrySkip())
                        return false;
                }
            }

            return reader.TryReadToken(JsonTokenType.EndObject)
                && read == 1;
        }

        public static bool TryReadReady(ref Utf8JsonReader reader,
            out VoiceGatewayReady ready)
        {
            ready = default;

            if (!reader.TryReadToken(JsonTokenType.StartObject))
                return false;

            int read = 0;
            while (reader.TryReadToken(JsonTokenType.PropertyName))
            {
                if (reader.ValueTextEquals(SsrcPropertyName))
                {
                    if (!reader.TryReadToken(JsonTokenType.Number)
                        || !reader.TryGetUInt32(out var ssrc))
                        return false;

                    ready.Ssrc = ssrc;
                    read++;
                }
                else if (reader.ValueTextEquals(IpPropertyName))
                {
                    if (!reader.TryReadToken(JsonTokenType.String))
                        return false;

                    if (reader.HasValueSequence)
                        reader.ValueSequence.CopyTo(ready.RawIp);
                    else
                        reader.ValueSpan.CopyTo(ready.RawIp);

                    read++;
                }
                else if (reader.ValueTextEquals(PortPropertyName))
                {
                    if (!reader.TryReadToken(JsonTokenType.Number)
                        || !reader.TryGetInt32(out var port))
                        return false;

                    ready.Port = port;
                    read++;
                }
                else if (reader.ValueTextEquals(ModesPropertyName))
                {
                    if (!reader.TryReadToken(JsonTokenType.StartArray))
                        return false;

                    while (TryReadEncryptionMode(ref reader, out var mode))
                        ready.Modes |= mode;

                    if (!reader.TryReadToken(JsonTokenType.EndArray))
                        return false;

                    read++;
                }
                else
                {
                    if (!reader.TrySkip())
                        return false;
                }
            }

            return reader.TryReadToken(JsonTokenType.EndObject)
                && read == 4;

            static bool TryReadEncryptionMode(ref Utf8JsonReader reader,
                out EncryptionModes mode)
            {
                if (!reader.TryReadToken(JsonTokenType.String))
                {
                    mode = default;
                    return false;
                }

                if (reader.ValueTextEquals(XSalsa20Poly1305Mode))
                {
                    mode = EncryptionModes.XSalsa20_Poly1305;
                    return true;
                }

                if (reader.ValueTextEquals(XSalsa20Poly1305SuffixMode))
                {
                    mode = EncryptionModes.XSalsa20_Poly1305_Suffix;
                    return true;
                }

                if (reader.ValueTextEquals(XSalsa20Poly1305LiteMode))
                {
                    mode = EncryptionModes.XSalsa20_Poly1305_Lite;
                    return true;
                }

                mode = default;
                return false;
            }
        }

        public static bool TryReadSessionDescription(ref Utf8JsonReader reader,
            Span<byte> sessionEncryptionKey)
        {
            if (!reader.TryReadToken(JsonTokenType.StartObject))
                return false;

            int read = 0;
            while (reader.TryReadToken(JsonTokenType.PropertyName))
            {
                if (reader.ValueTextEquals(SecretKeyPropertyName))
                {
                    if (!reader.TryReadToken(JsonTokenType.StartArray))
                        return false;

                    for (int i = 0; i < sessionEncryptionKey.Length; i++)
                    {
                        if (!reader.TryReadToken(JsonTokenType.Number)
                            || !reader.TryGetByte(out var value))
                            return false;

                        sessionEncryptionKey[i] = value;
                    }

                    if (!reader.TryReadToken(JsonTokenType.EndArray))
                        return false;

                    read++;
                }
                else
                {
                    if (!reader.TrySkip())
                        return false;
                }
            }

            return reader.TryReadToken(JsonTokenType.EndObject)
                && read == 1;
        }
    }
}
