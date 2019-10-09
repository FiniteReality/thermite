using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Thermite.Discord.Models;
using Thermite.Internal;
using Thermite.Utilities;

using static Thermite.Utilities.State;

namespace Thermite.Discord
{
    internal partial class VoiceGatewayClient
    {
        private const int Connecting = 2;
        private const int Connected = 3;
        private const int Ready = 4;
        private const int Disconnecting = 5;

        private readonly SemaphoreSlim _heartbeatMutex;
        private readonly Pipe _receivePipe;
        private readonly Pipe _sendPipe;
        private readonly ulong _userId;
        private readonly ClientWebSocket _websocket;

        private State _state;

        private TimeSpan _heartbeatInterval;
        private int _nonce;

        public IPEndPoint? EndPoint { get; private set; }
        public IPEndPoint? ClientEndPoint { get; private set; }

        public VoiceGatewayClient(ulong userId, IPEndPoint? clientEndPoint)
        {
            _heartbeatMutex = new SemaphoreSlim(initialCount: 1);
            _receivePipe = new Pipe();
            _sendPipe = new Pipe();
            _userId = userId;
            _websocket = new ClientWebSocket();

            ClientEndPoint = clientEndPoint;

            _state.Transition(to: Initialized);
        }

        private ValueTask<bool> TryReadPacketAsync(PipeWriter writer,
            ReadOnlySequence<byte> sequence, out SequencePosition consumed)
        {
            consumed = default;
            var reader = new Utf8JsonReader(sequence);
            VoiceGatewayOpcode opcode = VoiceGatewayOpcode.Unknown;
            SequencePosition startPosition = default;
            SequencePosition endPosition = default;

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(OpcodePropertyName):
                        if (reader.Read() && reader.TryGetInt32(
                            out var op))
                        {
                            opcode = (VoiceGatewayOpcode)op;
                        }
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(DataPropertyName):
                        startPosition = reader.Position;
                        reader.Skip();
                        endPosition = reader.Position;
                        break;

                    case JsonTokenType.EndObject
                        when reader.CurrentDepth < 1:
                        consumed = reader.Position;
                        break;
                }
            }

            var data = sequence.Slice(startPosition, endPosition);
            switch (opcode)
            {
                case VoiceGatewayOpcode.Hello:
                    if (!Payload.TryReadHello(data, out var hello))
                        return new ValueTask<bool>(false);

                    _heartbeatInterval = hello.HeartbeatInterval;
                    return SendIdentifyAsync(writer);

                case VoiceGatewayOpcode.Ready:
                    if (!Payload.TryReadReady(data, out var ready))
                        return new ValueTask<bool>(false);

                    if (!IPUtility.TryParseAddress(ready.Ip,
                        out var address))

                    EndPoint = new IPEndPoint(address, ready.Port);

                    if (_state.TryTransition(from: Connecting, to: Ready) != Connecting)
                        return new ValueTask<bool>(false);

                    if (ClientEndPoint == null)
                        return PerformDiscoveryAndSelectProtocolAsync(writer, ready);
                    else
                        return SendSelectProtocolAsync(writer, ready);

                default:
                    return new ValueTask<bool>(false);
            }
        }

        private ValueTask<bool> SendHeartbeatAsync(PipeWriter writer, int nonce)
        {
            return new ValueTask<bool>(false);
        }

        private ValueTask<bool> SendIdentifyAsync(PipeWriter writer)
        {
            return new ValueTask<bool>(false);
        }

        private static readonly byte[] DiscoveryPacket = new byte[70];
        private readonly byte[] DiscoveryPacketResponse = new byte[70];
        private async ValueTask<bool> PerformDiscoveryAndSelectProtocolAsync(
            PipeWriter writer, VoiceGatewayReady ready,
            CancellationToken cancellationToken = default)
        {
            using var socket = new Socket(AddressFamily.InterNetwork,
                SocketType.Dgram, ProtocolType.Udp);

            var bytesSent = await socket.SendToAsync(
                DiscoveryPacket, SocketFlags.None, EndPoint)
                .ConfigureAwait(false);

            if (bytesSent != 70)
                return false;

            var result = await socket.ReceiveFromAsync(
                DiscoveryPacketResponse, SocketFlags.None, EndPoint)
                .ConfigureAwait(false);

            if (result.ReceivedBytes != 70)
                return false;

            if (!TryGetLocalEndPoint(DiscoveryPacketResponse, out var endpoint))
                return false;

            ClientEndPoint = endpoint;

            return await SendSelectProtocolAsync(writer, ready)
                .ConfigureAwait(false);

            static bool TryGetLocalEndPoint(Span<byte> buffer,
                out IPEndPoint endPoint)
            {
                endPoint = default!;

                // split ip and port into separate spans
                var addressBuffer = buffer.Slice(4, 70 - 4 - 2);
                var portBuffer = buffer.Slice(buffer.Length - 2);

                if (!IPUtility.TryParseAddress(addressBuffer, out var address))
                    return false;

                if (!BinaryPrimitives.TryReadUInt16LittleEndian(portBuffer,
                    out ushort port))
                    return false;

                endPoint = new IPEndPoint(address, port);
                return true;
            }
        }

        private ValueTask<bool> SendSelectProtocolAsync(PipeWriter writer,
            VoiceGatewayReady ready)
        {
            return new ValueTask<bool>(false);
        }
    }
}