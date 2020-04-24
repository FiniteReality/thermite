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
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TerraFX.Utilities;
using Thermite.Discord.Models;
using Thermite.Internal;
using Thermite.Utilities;

using static TerraFX.Utilities.State;
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
        private readonly ILogger _logger;
        private readonly Pipe _receivePipe;
        private readonly Channel<ArrayBufferWriter<byte>> _sendChannel;
        private readonly IMemoryOwner<byte> _sessionEncryptionKey;
        private readonly UserToken _userInfo;
        private readonly ClientWebSocket _websocket;
        private readonly Utf8JsonWriter _writer;

        private State _state;
        private CancellationTokenSource? _stopCancelTokenSource;
        private TimeSpan _heartbeatInterval;
        private int _nonce;
        private SpinLock _writerSpinLock;

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
            IPEndPoint? clientEndPoint,
            ILogger clientLogger)
        {
            _discoverySocket = discoverySocket;
            _heartbeatMutex = new SemaphoreSlim(initialCount: 0);

            _logger = clientLogger;

            _receivePipe = new Pipe();
            _receivePipe.Reader.Complete();
            _receivePipe.Writer.Complete();

            _sendChannel = Channel.CreateBounded<ArrayBufferWriter<byte>>(
                new BoundedChannelOptions(10)
                {
                    AllowSynchronousContinuations = true,
                    SingleReader = true,
                    SingleWriter = false,
                    FullMode = BoundedChannelFullMode.DropOldest
                });

            _sessionEncryptionKey
                = MemoryPool<byte>.Shared.Rent(SessionEncryptionKeyLength);
            _userInfo = userInfo;
            _websocket = new ClientWebSocket();

            _writer = new Utf8JsonWriter(NullBufferWriter<byte>.Instance);
            _writerSpinLock = new SpinLock();

            ClientEndPoint = clientEndPoint;

            _ = _state.Transition(to: Initialized);
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
                _sessionEncryptionKey.Dispose();
                _websocket.Dispose();
                await _writer.DisposeAsync();
                _userInfo.Dispose();

                _state.EndDispose();
            }
        }

        public async Task SetSpeakingAsync(bool speaking, int delay = 0)
        {
            if (_state != Running)
                ThrowInvalidOperationException(
                    "Must be connected to set speaking");

            _logger.LogDebug("Set Speaking = {Speaking}, Delay = {Delay}",
                speaking, delay);

            await SendSpeakingAsync(speaking, delay, Ssrc);
        }

        private ValueTask<bool> TryProcessPacketAsync(
            ref ReadOnlySequence<byte> sequence,
            CancellationToken cancellationToken = default)
        {
            var reader = new Utf8JsonReader(sequence);

            VoiceGatewayPayload payload = default;

            if (!Payload.TryReadOpcode(ref reader, out payload.Opcode))
                return new ValueTask<bool>(false);

            switch (payload.Opcode)
            {
                case VoiceGatewayOpcode.Hello:
                {
                    if (!Payload.TryReadHello(ref reader, out payload.Hello))
                        return new ValueTask<bool>(false);

                    break;
                }
                case VoiceGatewayOpcode.Ready:
                {
                    if (!Payload.TryReadReady(ref reader, out payload.Ready))
                        return new ValueTask<bool>(false);

                    break;
                }
                case VoiceGatewayOpcode.SessionDescription:
                {
                    if (!Payload.TryReadSessionDescription(ref reader,
                        _sessionEncryptionKey.Memory.Span.Slice(0,
                            SessionEncryptionKeyLength)))
                        return new ValueTask<bool>(false);

                    break;
                }
                case VoiceGatewayOpcode.HeartbeatAck:
                {
                    if (!Payload.TryReadHeartbeatAck(ref reader,
                        out payload.Nonce))
                        return new ValueTask<bool>(false);

                    break;
                }
                case VoiceGatewayOpcode.Speaking:
                {
                    return new ValueTask<bool>(reader.TrySkip());
                }
                default:
                {
                    return new ValueTask<bool>(reader.TrySkip());
                }
            }

            if (!reader.TryReadToken(JsonTokenType.EndObject))
                return new ValueTask<bool>(false);

            sequence = sequence.Slice(reader.Position);

            _logger.LogTrace("Received opcode {Opcode}", payload.Opcode);

            return TryProcessOpcodeAsync(ref payload, cancellationToken);
        }

        private ValueTask<bool> TryProcessOpcodeAsync(
            ref VoiceGatewayPayload payload,
            CancellationToken cancellationToken = default)
        {
            switch (payload.Opcode)
            {
                case VoiceGatewayOpcode.Hello:
                {
                    _heartbeatInterval = payload.Hello.HeartbeatInterval;
                    _ = _heartbeatMutex.Release();

                    return SendIdentifyAsync(cancellationToken);
                }
                case VoiceGatewayOpcode.Ready:
                {
                    if (!Utf8IpAddressUtilities.TryParseAddress(
                        payload.Ready.SlicedIp, out var address))
                        return new ValueTask<bool>(false);

                    var remote = new IPEndPoint(address, payload.Ready.Port);
                    ClientSsrcUpdated?.Invoke(this, payload.Ready.Ssrc);
                    Ssrc = payload.Ready.Ssrc;
                    RemoteEndPointUpdated?.Invoke(this, remote);
                    EndPoint = remote;

                    return ClientEndPoint != null
                        ? SendSelectProtocolAsync(
                            payload.Ready, cancellationToken)
                        : HandleTaskAsync(
                            PerformDiscoveryAndSelectProtocolAsync(
                                payload.Ready, cancellationToken));
                }
                case VoiceGatewayOpcode.SessionDescription:
                {
                    SessionEncryptionKeyUpdated?.Invoke(this,
                        _sessionEncryptionKey.Memory.Slice(0,
                            SessionEncryptionKeyLength));

                    Ready?.Invoke(this, null!);
                    return new ValueTask<bool>(true);
                }
                case VoiceGatewayOpcode.HeartbeatAck:
                {
                    Debug.Assert(_nonce == payload.Nonce + 1,
                        "Nonces didn't match");
                    return new ValueTask<bool>(true);
                }
            }

            return new ValueTask<bool>(false);

            static async ValueTask<bool> HandleTaskAsync(Task asyncTask)
            {
                await asyncTask;

                // If the task isn't a Task<bool>, it likely wrote to the Pipe
                // via JsonSerializer. This is likely always going to succeed.
                // Otherwise, it likely performs some other operation which can
                // fail (e.g. IP discovery) - in which case we should return
                // its success values.

                return !(asyncTask is Task<bool> taskReturningBool)
                    || taskReturningBool.Result;
            }
        }
    }
}
