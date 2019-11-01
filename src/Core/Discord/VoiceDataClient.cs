using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using static Thermite.Natives.Opus;
using static System.Buffers.Binary.BinaryPrimitives;
using static Thermite.Natives.Sodium;

namespace Thermite.Discord
{
    internal class VoiceDataClient : IAsyncDisposable
    {
        private const ushort RtpHeader = 0x8078;
        private const int SamplingRate = 48000;

        private static readonly int AuthenticationCodeSize =
            (int)crypto_secretbox_macbytes().ToUInt32();
        private static readonly int NonceSize =
            (int)crypto_secretbox_noncebytes().ToUInt32();

        private static readonly int PacketHeadersSize =
            12 + AuthenticationCodeSize + 4; // rtp + mac + nonce

        private static readonly byte[] SilenceBuffer =
            new byte[]{ 0x03, 0x00, 0xF8, 0xFF, 0xFE };

        private readonly VoiceGatewayClient _gateway;
        private readonly Pipe _inputPipe;
        private readonly Timer _sendTimer;
        private readonly Socket _socket;

        private int _failedReadAttempts;
        private ushort _packetSequence;
        private uint _timestamp;

        private uint _ssrc;
        private IPEndPoint? _remoteEndPoint;
        private ReadOnlyMemory<byte>? _sessionEncryptionKey;

        public PipeWriter Writer => _inputPipe.Writer;

        public VoiceDataClient(VoiceGatewayClient gateway, Socket socket)
        {
            _gateway = gateway;
            _inputPipe = new Pipe();
            _sendTimer = new Timer(Process, this,
                Timeout.Infinite, Timeout.Infinite);
            _socket = socket;

            gateway.ClientSsrcUpdated += (_, ssrc) =>
            {
                _ssrc = ssrc;
            };

            gateway.RemoteEndPointUpdated += (_, endpoint) =>
            {
                _remoteEndPoint = endpoint;
            };

            gateway.SessionEncryptionKeyUpdated += (_, key) =>
            {
                _sessionEncryptionKey = key;
            };
        }

        public ValueTask DisposeAsync()
        {
            return _sendTimer.DisposeAsync();
        }

        public void Start()
        {
            _sendTimer.Change(0, Timeout.Infinite);
        }

        private static void Process(object? state)
        {
            var client = (VoiceDataClient)state!;
            var reader = client._inputPipe.Reader;
            var socket = client._socket;
            var timer = client._sendTimer;

            ref var sequence = ref client._packetSequence;
            ref var timestamp = ref client._timestamp;
            ref var failedReadAttempts = ref client._failedReadAttempts;

            var ssrc = client._ssrc;
            var endpoint = client._remoteEndPoint!;
            var encryptionKey = client._sessionEncryptionKey!.Value.Span;

            ReadOnlySequence<byte> buffer =
                new ReadOnlySequence<byte>(SilenceBuffer);
            var readSuccess = reader.TryRead(out var readResult);

            if (!readSuccess)
            {
                // adapt to potential buffer underruns in the underlying pipe
                if (failedReadAttempts++ < 3)
                {
                    timer.Change(1, Timeout.Infinite);
                    return;
                }
            }
            else
            {
                failedReadAttempts = 0;
                buffer = readResult.Buffer;
            }

            if (!TryReadPacket(ref buffer, out var packet))
                return;

            var length = PacketHeadersSize + (int)packet.Length;
            var pooledPacketBuffer = ArrayPool<byte>.Shared.Rent(length);
            var packetBuffer = pooledPacketBuffer.AsSpan().Slice(0, length);

            if (!TryEncodePacket(packet, packetBuffer, encryptionKey, sequence,
                timestamp, ssrc, out var frameSize))
                return;

            if (readSuccess)
                reader.AdvanceTo(buffer.Start, buffer.Start);

            unchecked
            {
                sequence++;
                timestamp += (uint)frameSize;
            }


            if (!readResult.IsCompleted)
                timer.Change(frameSize switch
                {
                    // known opus frame sizes at 48khz (discord audio)
                    // and their length in ms
                    120 => 2,
                    240 => 5,
                    480 => 10,
                    960 => 20,
                    1920 => 40,
                    2880 => 60,
                    _ => Timeout.Infinite
                }, Timeout.Infinite);

            var bytesSent = socket.SendTo(pooledPacketBuffer, 0, length,
                SocketFlags.None, endpoint);
            ArrayPool<byte>.Shared.Return(pooledPacketBuffer);

            static bool TryReadPacket(
                ref ReadOnlySequence<byte> sequence,
                out ReadOnlySequence<byte> packet)
            {
                packet = default;
                var reader = new SequenceReader<byte>(sequence);

                if (!reader.TryReadLittleEndian(out short packetLength))
                    return false;

                if (sequence.Length < packetLength)
                    return false;

                packet = sequence.Slice(reader.Position, packetLength);
                sequence = packet.Slice(reader.Position)
                    .Slice(packetLength);
                return true;
            }

            static unsafe bool TryEncodePacket(
                ReadOnlySequence<byte> opus,
                Span<byte> packet,
                ReadOnlySpan<byte> encryptionKey,
                ushort sequence,
                uint timestamp,
                uint ssrc,
                out int frameSize)
            {
                frameSize = default;

                if (!TryWriteUInt16BigEndian(packet, RtpHeader))
                    return false;
                if (!TryWriteUInt16BigEndian(packet.Slice(2), sequence))
                    return false;
                if (!TryWriteUInt32BigEndian(packet.Slice(4), timestamp))
                    return false;
                if (!TryWriteUInt32BigEndian(packet.Slice(8), ssrc))
                    return false;

                opus.CopyTo(packet.Slice(12));

                fixed (byte* data = packet.Slice(12))
                    frameSize = opus_packet_get_samples_per_frame(data,
                        SamplingRate);

                Span<byte> nonce = stackalloc byte[NonceSize];

                fixed (byte* nonceBytes = nonce)
                    randombytes_buf(nonceBytes, (UIntPtr)4);

                nonce.Slice(0, 4)
                    .CopyTo(packet.Slice(packet.Length - 4));

                int status;
                fixed (byte* data = packet.Slice(12))
                fixed (byte* nonceBytes = nonce)
                fixed (byte* key = encryptionKey)
                    status = crypto_secretbox_easy(data, data,
                        (ulong)opus.Length, nonceBytes, key);

                return status == 0;
            }
        }
    }
}