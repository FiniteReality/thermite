using System;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger _logger;
        private readonly PlayerManager _manager;
        private readonly Channel<TrackInfo> _trackQueue;

        private State _state;

        private CancellationTokenSource? _stopCancelTokenSource;
        private Task? _runTask;

        public event EventHandler<IPEndPoint> ClientEndPointUpdated
        {
            add { _gatewayClient.ClientEndPointUpdated += value; }
            remove { _gatewayClient.ClientEndPointUpdated -= value; }
        }

        public event EventHandler<UnobservedTaskExceptionEventArgs>?
            ProcessingException;

        private PipeWriter Writer => _dataClient.Writer;

        /// <inheritdoc/>
        public TrackInfo CurrentTrack { get; private set; }

        public Player(PlayerManager manager, ILogger logger,
            UserToken userInfo, Socket socket, Uri endpoint,
            IPEndPoint? clientEndPoint)
        {
            _connectMutex = new SemaphoreSlim(initialCount: 0);
            _endpoint = endpoint;
            _gatewayClient = new VoiceGatewayClient(userInfo, socket,
                clientEndPoint);
            _dataClient = new VoiceDataClient(_gatewayClient, socket);
            _logger = logger;
            _manager = manager;
            _trackQueue = Channel.CreateUnbounded<TrackInfo>(QueueOptions);

            _gatewayClient.Ready += (_, __) => {
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

            if (_state.TryTransition(from: Initialized, to: Started)
                != Initialized)
                ThrowInvalidOperationException(
                    "Cannot start when already started");

            _logger.LogInformation("Starting player");

            _stopCancelTokenSource = new CancellationTokenSource();
            _runTask = RunAsync(_stopCancelTokenSource.Token);
        }

        public async Task StopAsync()
        {
            _state.ThrowIfDisposed(nameof(Player));

            if (_state != Started)
                ThrowInvalidOperationException("Cannot stop when not started");

            _logger.LogInformation("Stopping player");

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

                    return;
                }
            }

            ThrowInvalidUriException(nameof(location), location);
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

                _logger.LogInformation("Playing {title} ({url})",
                    info.TrackName, info.OriginalLocation);

                CurrentTrack = info;

                try
                {
                    await ProcessQueueItemAsync(info, cancellationToken);
                }
                catch (OperationCanceledException)
                    when (!cancellationToken.IsCancellationRequested)
                { /* expected for unsupported tracks */ }
                catch (Exception e)
                {
                    if (!(e is AggregateException aggregate))
                        aggregate = new AggregateException(e);

                    var args = new UnobservedTaskExceptionEventArgs(aggregate);
                    ProcessingException?.Invoke(this, args);

                    if (!args.Observed)
                    {
                        _logger.LogError(aggregate,
                            "Unhandled exception occured while processing " +
                            "queue. Track {url} may be skipped.",
                            info.OriginalLocation);
                    }
                }
            }
        }

        private async Task ProcessQueueItemAsync(
            TrackInfo info,
            CancellationToken cancellationToken = default)
        {
            if (!_manager.TryGetProviderFactory(info.AudioLocation,
                out var providerFactory))
                return;

            using var sessionCancelToken = new CancellationTokenSource();
            using var linkedCancelToken = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken,
                    sessionCancelToken.Token);

            await using var provider = providerFactory
                .GetProvider(info.AudioLocation);

            var providerTask = provider.RunAsync(linkedCancelToken.Token);
            var mediaType = info.MediaTypeOverride ??
                await provider.IdentifyMediaTypeAsync(
                    linkedCancelToken.Token);

            if (!_manager.TryGetDecoderFactory(mediaType,
                out var decoderFactory))
            {
                _logger.LogWarning(
                    "Could not retrieve decoder for track {url}. Skipping.",
                    info.OriginalLocation);

                sessionCancelToken.Cancel();
                await providerTask;
                return;
            }

            await using var decoder = decoderFactory.GetDecoder(
                provider.Output);

            var decoderTask = decoder.RunAsync(linkedCancelToken.Token);
            var codec = info.CodecOverride ??
                await decoder.IdentifyCodecAsync(linkedCancelToken.Token);

            if (codec == null)
            {
                _logger.LogWarning(
                    "Could not identify codec for track {url}. Skipping.",
                    info.OriginalLocation);

                sessionCancelToken.Cancel();
                await Task.WhenAll(providerTask, decoderTask);
                return;
            }

            var pipeline = await TranscoderPipeline
                .CreatePipelineAsync(_manager, decoder.Output, codec,
                    linkedCancelToken.Token);

            if (pipeline == null)
            {
                _logger.LogWarning(
                    "Could not build transcoder pipeline for track {url}. " +
                    "Skipping.",
                    info.OriginalLocation);

                sessionCancelToken.Cancel();
                await Task.WhenAll(providerTask, decoderTask);
                return;
            }

            try
            {
                await Task.WhenAll(
                    pipeline.RunAsync(linkedCancelToken.Token),
                    pipeline.Output.CopyToAsync(Writer,
                        linkedCancelToken.Token),
                    decoderTask,
                    providerTask);
            }
            finally
            {
                // Cancel our session token just in case any tasks are still
                // running.
                sessionCancelToken.Cancel();
            }
        }
    }
}
