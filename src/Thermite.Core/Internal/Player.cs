using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Voltaic;
using Wumpus;
using Wumpus.Events;
using Wumpus.Requests;

namespace Thermite.Core
{
    internal class Player : IPlayer, IDisposable
    {
        private static readonly byte[] SilenceFrame =
        {
            0xF8, 0xFF, 0xFE,
        };

        // Status events
        public event Action Connected;

        private readonly SemaphoreSlim _stateLock;
        private readonly WumpusAudioGatewayClient _gateway;
        private AudioQueue _queue;

        // Instance
        private Task _connectionTask;
        private CancellationTokenSource _runCts;

        // Run (Start/Stop)
        private ITokenProvider _tokenProvider;
        private WumpusAudioDataClient _data;
        private Snowflake _channelId;

        // Connection (For each voice connection)
        private TaskCompletionSource<bool> _readyReceivedPromise;
        private TaskCompletionSource<bool> _queueItemAdded;
        private BlockingCollection<AudioFrame> _sendQueue;
        private IPEndPoint _endPoint;
        private byte[] _secret;
        private uint _ssrc;
        private ushort _sequence;
        private uint _samplePosition;

        public PlayerState State { get; }

        public IAudioFile CurrentFile { get; private set; }

        public IAudioQueue Queue => _queue;

        public Player(Snowflake userId, Snowflake guildId)
        {
            _queue = new AudioQueue();
            _stateLock = new SemaphoreSlim(1, 1);
            _gateway = new WumpusAudioGatewayClient(userId, guildId);
            _connectionTask = Task.CompletedTask;
            _runCts = new CancellationTokenSource();
            _runCts.Cancel(); // Start canceled

            _gateway.VoiceGatewayReady += (ready) =>
            {
                _ssrc = ready.Ssrc;

                if (IPUtilities.TryParseIPv4Address(ready.IpAddress.Bytes,
                    out var address))
                    _endPoint = new IPEndPoint(address, ready.Port);

                _readyReceivedPromise.TrySetResult(true);
            };
            _gateway.VoiceSessionDescription += (description) =>
            {
                _secret = description.SecretKey;

                Connected?.Invoke();

                _gateway.Send(new VoiceGatewayPayload
                {
                    Operation = VoiceGatewayOperation.Speaking,
                    Data = new VoiceSpeakingParams
                    {
                        Speaking = (int)SpeakingState.Speaking,
                        DelayMilliseconds = 0,
                        Ssrc = _ssrc
                    }
                });
            };
        }

        public Task PausePlaybackAsync()
        {
            throw new System.NotImplementedException();
        }

        public Task ResumePlaybackAsync()
        {
            throw new System.NotImplementedException();
        }

        internal async Task RunAsync(Snowflake initialChannelId,
            ITokenProvider tokenProvider, WumpusAudioDataClient dataClient)
        {
            Task exceptionSignal;
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await StopAsyncInternal().ConfigureAwait(false);

                _tokenProvider = tokenProvider;
                _data = dataClient;
                _runCts = new CancellationTokenSource();
                _channelId = initialChannelId;

                _connectionTask = RunTaskAsync(_runCts.Token);
                exceptionSignal = _connectionTask;
            }
            finally
            {
                _stateLock.Release();
            }
            await exceptionSignal.ConfigureAwait(false);
        }

        private async Task RunTaskAsync(CancellationToken runCancelToken)
        {
            using (var connectionCts = new CancellationTokenSource())
            using (var cancelTokenCts = CancellationTokenSource
                .CreateLinkedTokenSource(runCancelToken, connectionCts.Token))
            {
                var cancelToken = cancelTokenCts.Token;

                while (true)
                {
                    cancelToken.ThrowIfCancellationRequested();

                    var info = await _tokenProvider
                        .GetTokenAsync(_gateway.UserId, _gateway.GuildId,
                            _channelId);

                    _readyReceivedPromise = new TaskCompletionSource<bool>();
                    _queueItemAdded = new TaskCompletionSource<bool>();
                    _data = new WumpusAudioDataClient(null);
                    // buffer approx 10s of audio
                    _sendQueue = new BlockingCollection<AudioFrame>(500);
                    _endPoint = null;
                    _sequence = 0;
                    _samplePosition = 0;

                    var task = await Task.WhenAny(
                        RunGatewayAsync(info, cancelToken),
                        RunSendAsync(cancelToken),
                        RunEventsAsync(cancelToken),
                        RunTranscodeAsync(cancelToken)
                    ).ConfigureAwait(false);

                    await task.ConfigureAwait(false);
                }
            }
        }
        private Task RunGatewayAsync(TokenInfo info,
            CancellationToken cancellationToken)
        {
            void Stop()
            {
                _gateway.Stop();
            }

            return Task.Run(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using (cancellationToken.Register(Stop))
                    await _gateway.RunAsync(info.Endpoint, info.SessionId,
                        info.Token)
                        .ConfigureAwait(false);
            });
        }
        private Task RunSendAsync(CancellationToken cancelToken)
        {
            return Task.Run(async () =>
            {
                await WaitOnPromise(_readyReceivedPromise, cancelToken)
                    .ConfigureAwait(false);

                Task timer = Task.CompletedTask;
                while (true)
                {
                    cancelToken.ThrowIfCancellationRequested();

                    if (_queue.Count == 0 && _sendQueue.Count == 0)
                        await WaitOnPromise(_queueItemAdded, cancelToken)
                            .ConfigureAwait(false);

                    await timer.ConfigureAwait(false);

                    if (_sendQueue.TryTake(out var frame))
                    {
                        try
                        {
                            timer = Task.Delay(frame.Length, cancelToken);
                            await SendAsync(frame.Data.AsSegment(),
                                frame.Samples,
                                (int)frame.Length.TotalMilliseconds)
                                    .ConfigureAwait(false);
                        }
                        finally
                        {
                            frame.Data.Return();
                        }
                    }
                    else
                    {
                        timer = Task.Delay(20, cancelToken);
                        await SendAsync(
                            new ArraySegment<byte>(SilenceFrame), 3, 20)
                            .ConfigureAwait(false);
                    }
                }
            });
        }
        private Task RunEventsAsync(CancellationToken cancelToken)
        {
            return Task.Run(async () =>
            {
                await WaitOnPromise(_readyReceivedPromise, cancelToken)
                    .ConfigureAwait(false);

                // TODO: No need to re-discover our local address if it's known
                var localEndPoint = await _data.DiscoverAsync(_ssrc, _endPoint)
                    .ConfigureAwait(false);

                _gateway.Send(new VoiceGatewayPayload
                {
                    Operation = VoiceGatewayOperation.SelectProtocol,
                    Data = new VoiceSelectProtocolParams
                    {
                        TransportProtocol = (Utf8String)"udp",
                        Properties = new VoiceSelectProtocolConnectionProperties
                        {
                            EncryptionScheme = (Utf8String)"xsalsa20_poly1305_lite",
                            Ip = (Utf8String)localEndPoint.Address.ToString(),
                            Port = localEndPoint.Port
                        }
                    }
                });
            });
        }

        private Task RunTranscodeAsync(CancellationToken cancelToken)
        {
            return Task.Run(async () =>
            {
                bool playing = false;
                foreach (var file in _queue)
                {
                    cancelToken.ThrowIfCancellationRequested();

                    if (!playing)
                    {
                        _queueItemAdded.TrySetResult(true);
                        playing = true;
                    }

                    CurrentFile = file;
                    using (var stream = await file.GetStreamAsync()
                        .ConfigureAwait(false))
                    while (true)
                    {
                        var memory = new ResizableMemory<byte>(2 << 16);
                        try
                        {
                            var read = await stream.ReadAsync(
                                memory.RequestSegment(2<<16), cancelToken)
                                .ConfigureAwait(false);

                            // if we reach end of file, bail out to the next
                            // track
                            if (read.BytesRead == 0)
                                break;

                            memory.Advance(read.BytesRead);

                            _sendQueue.Add(new AudioFrame
                            {
                                Data = memory,
                                Length = read.FrameLength,
                                Samples = read.SampleCount
                            }, cancelToken);
                        }
                        // NOTE: not a finally because we transfer ownership!
                        catch
                        {
                            memory.Return();
                            throw;
                        }
                    }

                    if (_queue.Count == 0 && playing)
                    {
                        _queueItemAdded = new TaskCompletionSource<bool>();
                        playing = false;
                    }
                }
            });
        }

        private async Task<TResult> WaitOnPromise<TResult>(
            TaskCompletionSource<TResult> taskCompletionSource,
            CancellationToken cancelToken)
        {
            void SetCanceled()
            {
                taskCompletionSource.TrySetCanceled();
            }

            using (cancelToken.Register(SetCanceled))
            {
                return await taskCompletionSource.Task.ConfigureAwait(false);
            }
        }

        public async Task StopAsync()
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await StopAsyncInternal().ConfigureAwait(false);
            }
            finally
            {
                _stateLock.Release();
            }
        }

        private async Task StopAsyncInternal()
        {
            _runCts?.Cancel();

            try { await _connectionTask.ConfigureAwait(false); } catch { }
            _connectionTask = Task.CompletedTask;
        }

        private async Task SendAsync(ArraySegment<byte> data, uint samples,
            int frameMs)
        {
            await _data.SendAsync(_ssrc, _sequence,
                _samplePosition, data, _secret, _endPoint)
                .ConfigureAwait(false);

            _sequence++;
            _samplePosition += samples;
        }

        public void Dispose()
        {
            StopAsync().GetAwaiter().GetResult();
            _stateLock.Dispose();
            _queue.Finish();
            _gateway.Dispose();
        }
    }
}
