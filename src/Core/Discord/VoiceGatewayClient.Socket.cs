using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Thermite.Core;
using static Thermite.Utilities.State;
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
            _sendPipe.Reset();
            _serializer.Reset();
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

                    await _websocket.ConnectAsync(endpoint, cancellationToken)
                        .ConfigureAwait(false);

                    await Task.WhenAll(
                        RunHeartbeatAsync(_serializer, runCancelToken),
                        RunProcessAsync(_receivePipe.Reader, _serializer,
                            runCancelToken),
                        RunReceiveAsync(_receivePipe.Writer, runCancelToken),
                        RunSendAsync(_sendPipe.Reader, runCancelToken)
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
            _state.ThrowIfDisposed(nameof(VoiceGatewayClient));

            if (_state > Initialized)
            {
                _stopCancelTokenSource!.Cancel();
            }
        }

        private ValueTask<FlushResult> FlushWritePipe(
            CancellationToken cancellationToken = default)
        {
            _serializer.Reset();

            return _sendPipe.Writer.FlushAsync(cancellationToken);
        }

        private async Task RunHeartbeatAsync(Utf8JsonWriter writer,
            CancellationToken cancellationToken = default)
        {
            await _heartbeatMutex.WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            FlushResult result = default;
            while (!result.IsCompleted)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await Task.Delay(_heartbeatInterval, cancellationToken)
                    .ConfigureAwait(false);

                SendHeartbeat(writer, _nonce++);

                result = await FlushWritePipe(cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        private async Task RunProcessAsync(PipeReader reader,
            Utf8JsonWriter writer,
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
                        .ReadAsync(cancellationToken)
                        .ConfigureAwait(false);

                    Log?.Invoke(this, new LogMessage(
                        category: "VoiceGatewayClient.RunProcessAsync",
                        message: $"Read {readResult.Buffer.Length} bytes"));

                    var buffer = readResult.Buffer;
                    var consumed = buffer.Start;
                    var examined = buffer.End;

                    if (await TryProcessPacketAsync(writer,
                        ref buffer, cancellationToken))
                    {
                        consumed = buffer.Start;
                        examined = consumed;
                        var length = readResult.Buffer.Length - buffer.Length;
                        Log?.Invoke(this, new LogMessage(
                            category: "VoiceGatewayClient.RunProcessAsync",
                            message: $"Processed {length} bytes"));

                        if (_serializer.BytesCommitted > 0)
                            flushResult = await FlushWritePipe(
                                cancellationToken)
                                .ConfigureAwait(false);
                    }
                    else
                    {
                        Log?.Invoke(this, new LogMessage(
                            category: "VoiceGatewayClient.RunProcessAsync",
                            message: $"Failed to process message, waiting..."
                        ));
                    }

                    reader.AdvanceTo(consumed, examined);
                }
            }
            catch (OperationCanceledException)
            {
                Log?.Invoke(this, new LogMessage(
                    category: "VoiceGatewayClient.RunProcessAsync",
                    message: "WebSocket process task cancelled"));

                throw;
            }
            catch (Exception e)
            {
                Log?.Invoke(this, new LogMessage(
                    category: "VoiceGatewayClient.RunProcessAsync",
                    message: $"Unhandled exception {e.Message}"));

                throw;
            }
            finally
            {
                await reader.CompleteAsync()
                    .ConfigureAwait(false);
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
                            cancellationToken)
                            .ConfigureAwait(false);

                        writer.Advance(readResult.Count);

                        Log?.Invoke(this, new LogMessage(
                            category: "VoiceGatewayClient.RunReceiveAsync",
                            message: $"Read {readResult.Count} bytes"));
                    }

                    flushResult = await writer
                        .FlushAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                Log?.Invoke(this, new LogMessage(
                    category: "VoiceGatewayClient.RunReceiveAsync",
                    message: "WebSocket read task cancelled"));

                throw;
            }
            catch (Exception e)
            {
                Log?.Invoke(this, new LogMessage(
                    category: "VoiceGatewayClient.RunReceiveAsync",
                    message: $"Unhandled exception {e.Message}"));

                throw;
            }
            finally
            {
                await writer.CompleteAsync()
                    .ConfigureAwait(false);
            }
        }

        private async Task RunSendAsync(PipeReader reader,
            CancellationToken cancellationToken = default)
        {
            ReadResult readResult = default;
            try
            {
                while (!readResult.IsCompleted)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    readResult = await reader.ReadAsync(cancellationToken)
                        .ConfigureAwait(false);

                    Log?.Invoke(this, new LogMessage(
                        category: "VoiceGatewayClient.RunSendAsync",
                        message: $"Read {readResult.Buffer.Length} bytes"));

                    var buffer = readResult.Buffer;

                    long length = 0;
                    while (GetWholeMessage(ref buffer, out var message))
                    {
                        // TODO: support multi-segment messages
                        if (message.IsSingleSegment)
                        {
                            var text = System.Text.Encoding.UTF8.GetString(
                                message.First.Span);
                            Log?.Invoke(this, new LogMessage(
                                category: "VoiceGatewayClient.RunSendAsync",
                                message: $"SEND: {text}"));

                            await _websocket.SendAsync(message.First,
                                WebSocketMessageType.Text, true,
                                cancellationToken)
                                .ConfigureAwait(false);
                        }

                        length = message.Length;
                    }

                    reader.AdvanceTo(buffer.Start, buffer.End);

                    Log?.Invoke(this, new LogMessage(
                        category: "VoiceGatewayClient.RunSendAsync",
                        message: $"Wrote {length} bytes"));
                }
            }
            catch (OperationCanceledException)
            {
                Log?.Invoke(this, new LogMessage(
                    category: "VoiceGatewayClient.RunSendAsync",
                    message: "WebSocket write task cancelled"));

                throw;
            }
            catch (Exception e)
            {
                Log?.Invoke(this, new LogMessage(
                    category: "VoiceGatewayClient.RunSendAsync",
                    message: $"Unhandled exception {e.Message}"));

                throw;
            }
            finally
            {
                await reader.CompleteAsync()
                    .ConfigureAwait(false);
            }

            static bool GetWholeMessage(
                ref ReadOnlySequence<byte> sequence,
                out ReadOnlySequence<byte> message)
            {
                message = default;

                if (sequence.Length == 0)
                    return false;

                var reader = new Utf8JsonReader(sequence);

                if (!reader.TryReadToken(JsonTokenType.StartObject))
                    return false;

                if (!reader.TrySkip())
                    return false;

                message = sequence.Slice(sequence.Start, reader.Position);
                sequence = sequence.Slice(reader.Position);
                return true;
            }
        }
    }
}