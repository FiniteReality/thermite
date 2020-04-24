using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Thermite.Discord.Models;

using static Thermite.Utilities.ThrowHelpers;

namespace Thermite.Discord
{
    internal partial class VoiceGatewayClient : IAsyncDisposable
    {
        private const int DefaultBufferSize = 512;

        private ValueTask<bool> SendHeartbeatAsync(int nonce,
            CancellationToken cancellationToken = default)
        {
            bool lockTaken = false;

            _logger.LogTrace("Queueing Heartbeat");

            try
            {
                _writerSpinLock.Enter(ref lockTaken);

                var buffer = new ArrayBufferWriter<byte>(DefaultBufferSize);
                _writer.Reset(buffer);

                Payload.WriteHeartbeat(_writer, nonce);
                _writer.Flush();

                return QueuePayloadAsync(buffer, cancellationToken);
            }
            finally
            {
                if (lockTaken)
                    _writerSpinLock.Exit();
            }
        }

        private ValueTask<bool> SendIdentifyAsync(
            CancellationToken cancellationToken = default)
        {
            bool lockTaken = false;

            _logger.LogTrace("Queueing Identify");

            try
            {
                _writerSpinLock.Enter(ref lockTaken);

                var buffer = new ArrayBufferWriter<byte>(DefaultBufferSize);
                _writer.Reset(buffer);

                Payload.WriteIdentify(_writer, _userInfo.UserId,
                    _userInfo.GuildId, _userInfo.SessionId, _userInfo.Token);
                _writer.Flush();

                return QueuePayloadAsync(buffer, cancellationToken);
            }
            finally
            {
                if (lockTaken)
                    _writerSpinLock.Exit();
            }
        }

        private ValueTask<bool> SendSpeakingAsync(bool speaking, int delay,
            uint ssrc,
            CancellationToken cancellationToken = default)
        {
            bool lockTaken = false;

            _logger.LogTrace("Queueing Speaking");

            try
            {
                _writerSpinLock.Enter(ref lockTaken);

                var buffer = new ArrayBufferWriter<byte>(DefaultBufferSize);
                _writer.Reset(buffer);

                Payload.WriteSpeaking(_writer, speaking, delay, ssrc);
                _writer.Flush();

                return QueuePayloadAsync(buffer, cancellationToken);
            }
            finally
            {
                if (lockTaken)
                    _writerSpinLock.Exit();
            }
        }

        private ValueTask<bool> SendSelectProtocolAsync(
            VoiceGatewayReady ready,
            CancellationToken cancellationToken = default)
        {
            if (!ready.Modes.HasFlag(EncryptionModes.XSalsa20_Poly1305_Lite))
                ThrowInvalidOperationException(
                    "xsalsa20_poly1305_lite not supported");

            // ClientEndPoint should be definitely assigned by this point due
            // to discovery or from being passed in as a ctor param
            Debug.Assert(ClientEndPoint != null);

            bool lockTaken = false;

            _logger.LogTrace("Queueing SelectProtocol");

            try
            {
                _writerSpinLock.Enter(ref lockTaken);

                var buffer = new ArrayBufferWriter<byte>(DefaultBufferSize);
                _writer.Reset(buffer);

                Payload.WriteSelectProtocol(_writer, ClientEndPoint,
                    EncryptionModes.XSalsa20_Poly1305_Lite);
                _writer.Flush();

                return QueuePayloadAsync(buffer, cancellationToken);
            }
            finally
            {
                if (lockTaken)
                    _writerSpinLock.Exit();
            }
        }

        private ValueTask<bool> QueuePayloadAsync(
            ArrayBufferWriter<byte> bytesToQueue,
            CancellationToken cancellationToken = default)
        {
            var writer = _sendChannel.Writer;

            return writer.TryWrite(bytesToQueue)
                ? new ValueTask<bool>(true)
                : new ValueTask<bool>(SlowPathAsync(
                    writer, bytesToQueue, cancellationToken));

            static async Task<bool> SlowPathAsync(
                ChannelWriter<ArrayBufferWriter<byte>> writer,
                ArrayBufferWriter<byte> buffer,
                CancellationToken cancellationToken = default)
            {
                while (await writer.WaitToWriteAsync(cancellationToken))
                {
                    if (writer.TryWrite(buffer))
                        return true;
                }

                return false;
            }
        }
    }
}
