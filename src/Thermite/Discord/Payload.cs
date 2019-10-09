using System;
using System.Buffers;
using System.Text;
using System.Text.Json;
using Thermite.Discord.Models;

namespace Thermite.Discord
{
    internal static class Payload
    {
        private static readonly byte[] HeartbeatIntervalPropertyName =
            Encoding.UTF8.GetBytes("heartbeat_interval");
        public static bool TryReadHello(ReadOnlySequence<byte> data,
            out VoiceGatewayHello hello)
        {
            var reader = new Utf8JsonReader(data);
            hello = default;

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(
                            HeartbeatIntervalPropertyName):

                        if (!reader.Read())
                            return false;

                        if (!reader.TryGetInt32(out var interval))
                            return false;

                        hello.HeartbeatInterval
                            = TimeSpan.FromMilliseconds(interval);
                        return true;
                }
            }

            return false;
        }

        private static readonly byte[] SsrcPropertyName =
            Encoding.UTF8.GetBytes("ssrc");
        private static readonly byte[] IpPropertyName =
            Encoding.UTF8.GetBytes("ip");
        private static readonly byte[] PortPropertyName =
            Encoding.UTF8.GetBytes("port");
        private static readonly byte[] ModesPropertyName =
            Encoding.UTF8.GetBytes("modes");
        public static bool TryReadReady(ReadOnlySequence<byte> data,
            out VoiceGatewayReady ready)
        {
            var reader = new Utf8JsonReader(data);
            ready = default;

            int read = 0;
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(SsrcPropertyName):

                        if (!reader.Read())
                            return false;

                        if (!reader.TryGetInt32(out var ssrc))
                            return false;

                        ready.Ssrc = ssrc;
                        read++;
                        break;

                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(IpPropertyName):

                        if (!reader.Read())
                            return false;

                        if (!reader.HasValueSequence)
                            return false;

                        ready.Ip = reader.ValueSequence;
                        read++;
                        break;

                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(PortPropertyName):

                        if (!reader.Read())
                            return false;

                        if (!reader.TryGetInt32(out var port))
                            return false;

                        ready.Port = port;
                        read++;
                        break;

                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(ModesPropertyName):

                        if (!reader.Read())
                            return false;

                        var start = reader.Position;

                        if (!reader.TrySkip())
                            return false;

                        var finish = reader.Position;

                        ready.Modes = data.Slice(start, finish);
                        read++;
                        break;
                }
            }

            return read == 4;
        }
    }
}