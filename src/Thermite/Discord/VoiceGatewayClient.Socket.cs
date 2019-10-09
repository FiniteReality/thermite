using System;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using static Thermite.Utilities.State;
using static Thermite.Utilities.ThrowHelpers;

namespace Thermite.Discord
{
    internal partial class VoiceGatewayClient
    {
        public async ValueTask RunAsync(Uri endpoint,
            CancellationToken cancellationToken = default)
        {
            if (_state.TryTransition(from: Initialized, to: Connecting) != Initialized)
            {
                ThrowInvalidOperationException("Cannot run client when already running");
            }

            _receivePipe.Reset();
            _sendPipe.Reset();

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _state.Transition(to: Connecting);

                await _websocket.ConnectAsync(endpoint, cancellationToken)
                    .ConfigureAwait(false);

                _state.Transition(to: Connected);

                await Task.WhenAll(
                    RunHeartbeatAsync(_sendPipe.Writer, cancellationToken),
                    RunProcessAsync(_receivePipe.Reader, _sendPipe.Writer,
                        cancellationToken),
                    RunReceiveAsync(_receivePipe.Writer, cancellationToken),
                    RunSendAsync(_sendPipe.Reader, cancellationToken)
                );
            }
        }

        private async Task RunHeartbeatAsync(PipeWriter writer,
            CancellationToken cancellationToken = default)
        {
            await _heartbeatMutex.WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await Task.Delay(_heartbeatInterval)
                    .ConfigureAwait(false);

                await SendHeartbeatAsync(writer, _nonce++)
                    .ConfigureAwait(false);
            }
        }

        private static readonly byte[] OpcodePropertyName = Encoding.UTF8.GetBytes("op");
        private static readonly byte[] DataPropertyName = Encoding.UTF8.GetBytes("d");
        private async Task RunProcessAsync(PipeReader reader, PipeWriter writer,
            CancellationToken cancellationToken = default)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var readResult = await reader
                    .ReadAsync(cancellationToken)
                    .ConfigureAwait(false);

                var status = await TryReadPacketAsync(writer, readResult.Buffer,
                    out var consumed);
                reader.AdvanceTo(consumed);

                if (!status)
                {
                    // TODO: log error, potentially disconnect, etc, etc
                }
            }
        }

        private async Task RunReceiveAsync(PipeWriter writer,
            CancellationToken cancellationToken = default)
        {
            FlushResult flushResult = default;
            while (!flushResult.IsCompleted)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ValueWebSocketReceiveResult readResult = default;
                while (!readResult.EndOfMessage)
                {
                    var memory = writer.GetMemory();
                    readResult = await _websocket.ReceiveAsync(memory,
                        cancellationToken)
                        .ConfigureAwait(false);

                    writer.Advance(readResult.Count);
                }

                flushResult = await writer
                    .FlushAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        private async Task RunSendAsync(PipeReader reader,
            CancellationToken cancellationToken = default)
        {
            ReadResult readResult = default;
            while (!readResult.IsCompleted)
            {
                cancellationToken.ThrowIfCancellationRequested();

                readResult = await reader.ReadAsync(cancellationToken)
                    .ConfigureAwait(false);

                foreach (var block in readResult.Buffer)
                {
                    await _websocket.SendAsync(block,
                        WebSocketMessageType.Text, true, cancellationToken)
                        .ConfigureAwait(false);
                }

                reader.AdvanceTo(readResult.Buffer.End);
            }
        }
    }
}