using System;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Thermite.Core;
using Thermite.Discord;
using Thermite.Utilities;

using static Thermite.Utilities.State;
using static Thermite.Utilities.ThrowHelpers;

namespace Thermite.Internal
{
    internal class Player : IPlayer, IAsyncDisposable
    {
        private const int Started = 2;

        private readonly SemaphoreSlim _connectMutex;
        private readonly VoiceDataClient _dataClient;
        private readonly Uri _endpoint;
        private readonly VoiceGatewayClient _gatewayClient;

        private State _state;

        private CancellationTokenSource? _stopCancelTokenSource;
        private Task? _runTask;

        public IPEndPoint? ClientEndPoint
            => _gatewayClient.ClientEndPoint;

        public event EventHandler<IPEndPoint> ClientEndPointUpdated
        {
            add { _gatewayClient.ClientEndPointUpdated += value; }
            remove { _gatewayClient.ClientEndPointUpdated -= value; }
        }

        public event EventHandler<LogMessage> Log
        {
            add { _gatewayClient.Log += value; }
            remove { _gatewayClient.Log -= value; }
        }

        public PipeWriter Writer => _dataClient.Writer;

        public Player(UserToken userInfo, Socket socket, Uri endpoint,
            IPEndPoint? clientEndPoint)
        {
            _connectMutex = new SemaphoreSlim(initialCount: 0);
            _endpoint = endpoint;
            _gatewayClient = new VoiceGatewayClient(userInfo, socket,
                clientEndPoint);
            _dataClient = new VoiceDataClient(_gatewayClient, socket);

            _gatewayClient.Ready += (_, __) =>
            {
                _connectMutex.Release();
            };

            _state.Transition(to: Initialized);
        }

        public async ValueTask DisposeAsync()
        {
            var priorState = _state.BeginDispose();
            if (priorState < Disposing)
            {
                if (priorState > Initialized)
                {
                    _gatewayClient.Stop();

                    try
                    {
                        await _runTask!.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        /* no-op */
                    }
                }

                await _gatewayClient.DisposeAsync()
                    .ConfigureAwait(false);

                await _dataClient.DisposeAsync()
                    .ConfigureAwait(false);

                _state.EndDispose();
            }
        }

        public void Start()
        {
            _state.ThrowIfDisposed(nameof(Player));

            if (_state.TryTransition(from: Initialized, to: Started) != Initialized)
                ThrowInvalidOperationException("Cannot start when already started");

            _stopCancelTokenSource = new CancellationTokenSource();
            _runTask = RunAsync(_stopCancelTokenSource.Token);
        }

        public async Task StopAsync()
        {
            _state.ThrowIfDisposed(nameof(Player));

            if (_state != Started)
                ThrowInvalidOperationException("Cannot stop when not started");

            // If we're running, then this is assigned
            _stopCancelTokenSource!.Cancel();
            await _runTask!.ConfigureAwait(false);
        }

        private async Task RunAsync(
            CancellationToken cancellationToken = default)
        {
            await Task.WhenAll(
                _gatewayClient.RunAsync(_endpoint, cancellationToken),
                RunDataClientAsync(cancellationToken),
                ProcessQueueAsync(cancellationToken)
            );
        }

        private async Task RunDataClientAsync(
            CancellationToken cancellationToken = default)
        {
            await _connectMutex.WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            await _gatewayClient.SetSpeakingAsync(true, 0)
                .ConfigureAwait(false);
            _dataClient.Start();
        }

        private Task ProcessQueueAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {

            });
        }
    }
}