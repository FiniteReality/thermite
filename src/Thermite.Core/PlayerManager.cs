using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wumpus;

namespace Thermite.Core
{
    public class PlayerManager
    {
        public const int DefaultSocketCount = 1000;

        private readonly ConcurrentDictionary<Snowflake, Player> _players;
        private readonly ImmutableArray<WumpusAudioDataClient> _udpClients;
        private readonly ConcurrentDictionary<Snowflake, Task> _runningTasks;

        private readonly SemaphoreSlim _stateLock;

        // Instance
        private CancellationTokenSource _runCts;

        // Run (Start/Stop)
        private TaskCompletionSource<bool> _clientException;
        private ITokenProvider _tokenProvider;

        public Snowflake UserId { get; }

        public PlayerManager(Snowflake userId, int maxUdpCount = DefaultSocketCount)
        {
            _stateLock = new SemaphoreSlim(1, 1);
            _runCts = new CancellationTokenSource();
            _runCts.Cancel();

            UserId = userId;

            var clients = ImmutableArray.CreateBuilder<WumpusAudioDataClient>();
            for (int i = 0; i < maxUdpCount; i++)
            {
                clients.Add(new WumpusAudioDataClient(null));
            }

            _players = new ConcurrentDictionary<Snowflake, Player>();
            _runningTasks = new ConcurrentDictionary<Snowflake, Task>();
            _udpClients = clients.ToImmutable();
        }

        public async Task RunAsync(ITokenProvider tokenProvider)
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);

            try
            {
                _tokenProvider = tokenProvider;
                _clientException = new TaskCompletionSource<bool>();
                _runCts = new CancellationTokenSource();

                var cancelTask = Task.Delay(-1, _runCts.Token);
                var tsk = await Task.WhenAny(_clientException.Task, cancelTask)
                        .ConfigureAwait(false);

                if (tsk != cancelTask)
                    await tsk;
            }
            finally
            {
                foreach (var player in _players.Values)
                {
                    await player.StopAsync()
                        .ConfigureAwait(false);
                }
                _stateLock.Release();
            }
        }

        public void Stop()
        {
            _runCts.Cancel();
        }

        private async Task<TResult> WaitOnPromise<TResult>(
            TaskCompletionSource<TResult> taskCompletionSource,
            CancellationToken cancelToken)
        {
            using (cancelToken.Register(
                () => taskCompletionSource.TrySetCanceled()))
            {
                return await taskCompletionSource.Task.ConfigureAwait(false);
            }
        }

        public async Task<IPlayer> GetOrCreatePlayerAsync(
            Snowflake guildId, Snowflake initialChannelId)
        {
            bool created = false;
            var player = _players.GetOrAdd(guildId, (x) =>
            {
                created = true;
                return new Player(UserId, guildId);
            });

            if (created)
            {
                var promise = new TaskCompletionSource<bool>();

                _runningTasks.TryAdd(guildId, Task.Run(async () =>
                {
                    Action handler = () => promise.TrySetResult(true);;

                    player.Connected += handler;
                    try
                    {
                        await player.RunAsync(initialChannelId,
                            _tokenProvider, GetUdpClientForGuildId(guildId))
                            .ConfigureAwait(false);
                    }
                    catch (TaskCanceledException)
                    { /* no-op */ }
                    catch (Exception e)
                    {
                        _clientException.TrySetException(e);
                    }
                    finally
                    {
                        player.Connected -= handler;
                    }
                }));

                await promise.Task.ConfigureAwait(false);
            }

            return player;
        }

        private WumpusAudioDataClient GetUdpClientForGuildId(Snowflake guildId)
        {
            var timestamp = (int)(guildId.RawValue >> 22);
            return _udpClients[timestamp % _udpClients.Length];
        }
    }
}