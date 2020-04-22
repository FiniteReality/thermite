using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using static TerraFX.Utilities.State;
using static Thermite.Utilities.ThrowHelpers;

namespace Thermite.Discord
{
    internal partial class VoiceGatewayClient
    {
        public async Task RunAsync(Uri endpoint,
            CancellationToken cancellationToken = default)
        {
            if (_state.TryTransition(from: Initialized, to: Running) != Initialized)
            {
                ThrowInvalidOperationException("Cannot run client when already running");
            }

            _receivePipe.Reset();
            _stopCancelTokenSource?.Dispose();
            _stopCancelTokenSource = new CancellationTokenSource();
            using var linkedTokenSource = CancellationTokenSource
                .CreateLinkedTokenSource(_stopCancelTokenSource.Token,
                cancellationToken);

            var runCancelToken = linkedTokenSource.Token;

            try
            {
                while (true)
                {
                    runCancelToken.ThrowIfCancellationRequested();

                    await _websocket.ConnectAsync(endpoint, cancellationToken);

                    await Task.WhenAll(
                        RunHeartbeatAsync(runCancelToken),
                        RunProcessAsync(_receivePipe.Reader, runCancelToken),
                        RunReceiveAsync(_receivePipe.Writer, runCancelToken),
                        RunSendAsync(_sendChannel.Reader, runCancelToken)
                    );
                }
            }
            catch (OperationCanceledException)
            {
                _ = _state.TryTransition(from: Running, to: Initialized);
                throw;
            }
        }

        public void Stop()
        {
            _state.ThrowIfDisposedOrDisposing();

            if (_state > Initialized)
            {
                _stopCancelTokenSource!.Cancel();
            }
        }

        private async Task RunHeartbeatAsync(
            CancellationToken cancellationToken = default)
        {
            await _heartbeatMutex.WaitAsync(cancellationToken);

            bool completed = false;
            while (!completed)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await Task.Delay(_heartbeatInterval, cancellationToken);

                completed = await SendHeartbeatAsync(
                    _nonce++, cancellationToken);
            }
        }

        private async Task RunProcessAsync(PipeReader reader,
            CancellationToken cancellationToken = default)
        {
            ReadResult readResult = default;
            FlushResult flushResult = default;
            try
            {
                while (!readResult.IsCompleted && !flushResult.IsCompleted)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    readResult = await reader
                        .ReadAsync(cancellationToken);

                    var buffer = readResult.Buffer;
                    var consumed = buffer.Start;
                    var examined = buffer.End;

                    if (await TryProcessPacketAsync(ref buffer,
                        cancellationToken))
                    {
                        consumed = buffer.Start;
                        examined = consumed;
                    }

                    reader.AdvanceTo(consumed, examined);
                }
            }
            finally
            {
                await reader.CompleteAsync();
            }
        }

        private async Task RunReceiveAsync(PipeWriter writer,
            CancellationToken cancellationToken = default)
        {
            FlushResult flushResult = default;
            try
            {
                while (!flushResult.IsCompleted)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    ValueWebSocketReceiveResult readResult = default;
                    while (!readResult.EndOfMessage)
                    {
                        var memory = writer.GetMemory();
                        readResult = await _websocket.ReceiveAsync(memory,
                            cancellationToken);

                        writer.Advance(readResult.Count);
                    }

                    flushResult = await writer
                        .FlushAsync(cancellationToken);
                }
            }
            finally
            {
                await writer.CompleteAsync();
            }
        }

        private async Task RunSendAsync(
            ChannelReader<ArrayBufferWriter<byte>> reader,
            CancellationToken cancellationToken = default)
        {
            while (await reader.WaitToReadAsync(cancellationToken))
            {
                if (reader.TryRead(out var buffer))
                {
                    await _websocket.SendAsync(buffer.WrittenMemory,
                        WebSocketMessageType.Text, true, cancellationToken);
                }
            }
        }
    }
}
