using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
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
using static Thermite.Utilities.ThrowHelpers;

namespace Thermite.Discord
{
    internal partial class VoiceGatewayClient : IAsyncDisposable
    {
        // secret_key length from discord api docs
        private const int SessionEncryptionKeyLength = 32;

        private const int Running = 2;

        private readonly Socket _discoverySocket;
        private readonly SemaphoreSlim _heartbeatMutex;
        private readonly Pipe _receivePipe;
        private readonly Pipe _sendPipe;
        private readonly Utf8JsonWriter _serializer;
        private readonly IMemoryOwner<byte> _sessionEncryptionKey;
        private readonly UserToken _userInfo;
        private readonly ClientWebSocket _websocket;

        private State _state;


        private SpinLock _serializerLock;
        private CancellationTokenSource? _stopCancelTokenSource;
        private TimeSpan _heartbeatInterval;
        private int _nonce;

        public IPEndPoint? EndPoint { get; private set; }
        public IPEndPoint? ClientEndPoint { get; private set; }
        public uint Ssrc { get; private set; }

        public event EventHandler<IPEndPoint>? ClientEndPointUpdated;
        public event EventHandler<uint>? ClientSsrcUpdated;
        public event EventHandler<IPEndPoint>? RemoteEndPointUpdated;
        public EventHandler? Ready;
        public event EventHandler<ReadOnlyMemory<byte>>? SessionEncryptionKeyUpdated;

        public ReadOnlySpan<byte> SessionEncryptionKey
            => _sessionEncryptionKey.Memory.Span.Slice(0,
                SessionEncryptionKeyLength);

        public VoiceGatewayClient(UserToken userInfo, Socket discoverySocket,
            IPEndPoint? clientEndPoint, MemoryPool<byte>? memoryPool = default)
        {
            memoryPool ??= MemoryPool<byte>.Shared;

            _discoverySocket = discoverySocket;
            _heartbeatMutex = new SemaphoreSlim(initialCount: 0);

            _receivePipe = new Pipe();
            _receivePipe.Reader.Complete();
            _receivePipe.Writer.Complete();

            _sendPipe = new Pipe();
            _sendPipe.Reader.Complete();
            _sendPipe.Writer.Complete();

            _serializer = new Utf8JsonWriter(_sendPipe.Writer);
            _serializerLock = new SpinLock();
            _sessionEncryptionKey = memoryPool.Rent(SessionEncryptionKeyLength);
            _userInfo = userInfo;
            _websocket = new ClientWebSocket();

            ClientEndPoint = clientEndPoint;

            _state.Transition(to: Initialized);
        }

        public async ValueTask DisposeAsync()
        {
            var priorState = _state.BeginDispose();
            if (priorState < Disposing)
            {
                if (priorState > Initialized)
                {
                    // if we started running, ensure we are stopped before
                    // continuing
                    _stopCancelTokenSource!.Cancel();
                    _stopCancelTokenSource!.Dispose();
                }

                _heartbeatMutex.Dispose();
                await _serializer.DisposeAsync();
                _websocket.Dispose();
                _sessionEncryptionKey.Dispose();
                _userInfo.Dispose();

                _state.EndDispose();
            }
        }

        public async Task SetSpeakingAsync(bool speaking, int delay = 0)
        {
            if (_state != Running)
                ThrowInvalidOperationException(
                    "Must be connected to set speaking");

            SendSpeaking(_serializer, speaking, delay, Ssrc);

            await FlushWritePipe();
        }

        private ValueTask<bool> TryProcessPacketAsync(Utf8JsonWriter writer,
            ref ReadOnlySequence<byte> sequence,
            CancellationToken cancellationToken = default)
        {
            var reader = new Utf8JsonReader(sequence);

            if (!Payload.TryReadOpcode(ref reader, out var opcode))
                return new ValueTask<bool>(false);

            Task? asyncTask = null;
            switch (opcode)
            {
                case VoiceGatewayOpcode.Hello:
                    if (!Payload.TryReadHello(ref reader, out var hello))
                        return new ValueTask<bool>(false);
                    _heartbeatInterval = hello.HeartbeatInterval;
                    _heartbeatMutex.Release();
                    SendIdentify(writer);
                    break;

                case VoiceGatewayOpcode.Ready:
                    if (!Payload.TryReadReady(ref reader, out var ready))
                        return new ValueTask<bool>(false);

                    if (!IPUtility.TryParseAddress(ready.SlicedIp, out var address))
                        return new ValueTask<bool>(false);

                    var remote = new IPEndPoint(address, ready.Port);
                    ClientSsrcUpdated?.Invoke(this, ready.Ssrc);
                    Ssrc = ready.Ssrc;
                    RemoteEndPointUpdated?.Invoke(this, remote);
                    EndPoint = remote;

                    if (ClientEndPoint != null)
                    {
                        SendSelectProtocol(writer, ready);
                        break;
                    }

                    asyncTask = PerformDiscoveryAndSelectProtocolAsync(
                        writer, ready);
                    break;

                case VoiceGatewayOpcode.SessionDescription:
                    if (!Payload.TryReadSessionDescription(ref reader,
                        _sessionEncryptionKey.Memory.Span.Slice(0,
                            SessionEncryptionKeyLength)))
                        return new ValueTask<bool>(false);

                    SessionEncryptionKeyUpdated?.Invoke(this,
                        _sessionEncryptionKey.Memory.Slice(0,
                            SessionEncryptionKeyLength));
                    Ready?.Invoke(this, null!);
                    break;

                case VoiceGatewayOpcode.HeartbeatAck:
                    if (!Payload.TryReadHeartbeatAck(ref reader,
                        out var nonce))
                        return new ValueTask<bool>(false);

                    Debug.Assert(_nonce == nonce + 1, "Nonces didn't match");
                    break;

                case VoiceGatewayOpcode.Speaking:
                    if (!reader.TrySkip())
                        return new ValueTask<bool>(false);
                    break;

                default:
                    if (!reader.TrySkip())
                        return new ValueTask<bool>(false);

                    break;
            }

            if (!reader.TryReadToken(JsonTokenType.EndObject))
                return new ValueTask<bool>(false);

            sequence = sequence.Slice(reader.Position);
            if (asyncTask != null)
                return new ValueTask<bool>(HandleAsyncPart(asyncTask));

            return new ValueTask<bool>(true);

            static async Task<bool> HandleAsyncPart(Task asyncTask)
            {
                await asyncTask;

                // If the task isn't a Task<bool>, it likely wrote to the Pipe
                // via JsonSerializer. This is likely always going to succeed.
                // Otherwise, it likely performs some other operation which can
                // fail (e.g. IP discovery) - in which case we should return
                // its success values.

                if (asyncTask is Task<bool> taskReturningBool)
                    return taskReturningBool.Result;

                return true;
            }
        }

        private void SendHeartbeat(Utf8JsonWriter writer, int nonce)
        {
            bool lockTaken = false;

            try
            {
                _serializerLock.Enter(ref lockTaken);

                Payload.WriteHeartbeat(writer, nonce);
                writer.Flush();
            }
            finally
            {
                if (lockTaken)
                    _serializerLock.Exit();
            }
        }

        private void SendIdentify(Utf8JsonWriter writer)
        {
            bool lockTaken = false;

            try
            {
                _serializerLock.Enter(ref lockTaken);

                Payload.WriteIdentify(writer, _userInfo.UserId, _userInfo.GuildId,
                    _userInfo.SessionId, _userInfo.Token);
                writer.Flush();
            }
            finally
            {
                if (lockTaken)
                    _serializerLock.Exit();
            }
        }

        private void SendSpeaking(Utf8JsonWriter writer, bool speaking,
            int delay, uint ssrc)
        {
            bool lockTaken = false;

            try
            {
                _serializerLock.Enter(ref lockTaken);

                Payload.WriteSpeaking(writer, speaking, delay, ssrc);
                writer.Flush();
            }
            finally
            {
                if (lockTaken)
                    _serializerLock.Exit();
            }
        }

        private static readonly byte[] DiscoveryPacket = new byte[70];
        private readonly byte[] DiscoveryPacketResponse = new byte[70];
        private async Task<bool> PerformDiscoveryAndSelectProtocolAsync(
            Utf8JsonWriter writer, VoiceGatewayReady ready)
        {

            var bytesSent = await _discoverySocket.SendToAsync(
                DiscoveryPacket, SocketFlags.None, EndPoint);

            if (bytesSent != 70)
                return false;

            var result = await _discoverySocket.ReceiveFromAsync(
                DiscoveryPacketResponse, SocketFlags.None, EndPoint);

            if (result.ReceivedBytes != 70)
                return false;

            if (!TryGetLocalEndPoint(DiscoveryPacketResponse, out var endpoint))
                return false;

            ClientEndPointUpdated?.Invoke(this, endpoint);
            ClientEndPoint = endpoint;

            SendSelectProtocol(writer, ready);
            return true;

            static bool TryGetLocalEndPoint(Span<byte> buffer,
                out IPEndPoint endPoint)
            {
                endPoint = default!;

                // split ip and port into separate spans
                var addressBuffer = buffer.Slice(4, 64)
                    .TrimEnd((byte)0);
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

        private void SendSelectProtocol(Utf8JsonWriter writer,
            VoiceGatewayReady ready)
        {
            if (!ready.Modes.HasFlag(EncryptionModes.XSalsa20_Poly1305_Lite))
                ThrowInvalidOperationException(
                    "xsalsa20_poly1305_lite not supported");

            // ClientEndPoint is definitely assigned by this point due to
            // discovery or from being passed in as a ctor param

            bool lockTaken = false;

            try
            {
                _serializerLock.Enter(ref lockTaken);

                Payload.WriteSelectProtocol(writer, ClientEndPoint!,
                    EncryptionModes.XSalsa20_Poly1305_Lite);
                writer.Flush();
            }
            finally
            {
                _serializerLock.Exit();
            }
        }
    }
}
