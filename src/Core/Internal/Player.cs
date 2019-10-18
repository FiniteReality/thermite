using System;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
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

        private static readonly UnboundedChannelOptions QueueOptions =
            new UnboundedChannelOptions
            {
                AllowSynchronousContinuations = true,
                SingleReader = true,
                SingleWriter = false
            };

        private readonly SemaphoreSlim _connectMutex;
        private readonly VoiceDataClient _dataClient;
        private readonly Uri _endpoint;
        private readonly VoiceGatewayClient _gatewayClient;
        private readonly PlayerManager _manager;
        private readonly Channel<TrackInfo> _trackQueue;

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

        public PipeWriter Writer => _dataClient.Writer;

        public Player(PlayerManager manager, UserToken userInfo, Socket socket,
            Uri endpoint, IPEndPoint? clientEndPoint)
        {
            _connectMutex = new SemaphoreSlim(initialCount: 0);
            _endpoint = endpoint;
            _gatewayClient = new VoiceGatewayClient(userInfo, socket,
                clientEndPoint);
            _dataClient = new VoiceDataClient(_gatewayClient, socket);
            _manager = manager;
            _trackQueue = Channel.CreateUnbounded<TrackInfo>(QueueOptions);

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
                    _stopCancelTokenSource!.Cancel();

                    try
                    {
                        await _runTask!;
                    }
                    catch (OperationCanceledException)
                    {
                        /* no-op, this is intended */
                    }
                }

                await _gatewayClient.DisposeAsync();

                await _dataClient.DisposeAsync();

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
            await _runTask!;
        }

        /// <inheritdoc/>
        public async ValueTask EnqueueAsync(Uri location)
        {
            foreach (var source in _manager.Sources)
            {
                if (source.IsSupported(location))
                {
                    await foreach (var track in source
                        .GetTracksAsync(location))
                    {
                        await _trackQueue.Writer.WriteAsync(track);
                    }
                }
            }
        }

        /// <inheritdoc/>
        public ValueTask EnqueueAsync(TrackInfo track)
        {
            return _trackQueue.Writer.WriteAsync(track);
        }


        private async Task RunAsync(
            CancellationToken cancellationToken = default)
        {
            var oldContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);

            try
            {
                await Task.WhenAll(
                    ProcessQueueAsync(_trackQueue.Reader, cancellationToken),
                    RunDataClientAsync(cancellationToken),
                    _gatewayClient.RunAsync(_endpoint, cancellationToken)
                );
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(oldContext);
            }
        }

        private async Task RunDataClientAsync(
            CancellationToken cancellationToken = default)
        {
            await _connectMutex.WaitAsync(cancellationToken);
            await _gatewayClient.SetSpeakingAsync(true, 0);
            _dataClient.Start();
        }

        private async Task ProcessQueueAsync(
            ChannelReader<TrackInfo> queueReader,
            CancellationToken cancellationToken = default)
        {
            while (await queueReader.WaitToReadAsync(cancellationToken)
                .ConfigureAwait(false))
            {
                var info = await queueReader.ReadAsync(cancellationToken);

                if (!TryGetProviderFactory(info.AudioLocation,
                    out var providerFactory))
                    continue; // skip this track since we can't retrieve it

                var provider = await providerFactory
                    .GetProviderAsync(info.AudioLocation);
                var mediaType = info.MediaTypeOverride ??
                    await provider.IdentifyMediaTypeAsync();

                if (!TryGetTranscoderFactory(mediaType,
                    out var transcoderFactory))
                    continue; // skip this track since we can't transcode it

                var transcoder = transcoderFactory.GetTranscoder(
                    provider.Output);

                await Task.WhenAll(
                    transcoder.Output.CopyToAsync(Writer, cancellationToken),
                    provider.RunAsync(cancellationToken),
                    transcoder.RunAsync(cancellationToken));
            }
        }

        private bool TryGetProviderFactory(Uri location,
            [NotNullWhen(true)]out IAudioProviderFactory? factory)
        {
            foreach (var providerFactory in _manager.Providers)
            {
                if (providerFactory.IsSupported(location))
                {
                    factory = providerFactory;
                    return true;
                }
            }

            factory = default;
            return false;
        }

        private bool TryGetTranscoderFactory(string mediaType,
            [NotNullWhen(true)]out IAudioTranscoderFactory? factory)
        {
            foreach (var transcoderFactory in _manager.Transcoders)
            {
                if (transcoderFactory.IsSupported(mediaType))
                {
                    factory = transcoderFactory;
                    return true;
                }
            }

            factory = default;
            return false;
        }
    }
}