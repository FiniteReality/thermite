using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Thermite.Discord;
using Thermite.Internal;
using Thermite.Utilities;

using static Thermite.Utilities.State;

namespace Thermite.Core
{
    /// <summary>
    /// Manages instances of <see cref="IPlayer" />.
    /// </summary>
    public class PlayerManager : IAsyncDisposable
    {
        private static readonly string EndpointPrefix = "wss://";
        private static readonly byte[] EndpointPrefixU8 =
            Encoding.UTF8.GetBytes(EndpointPrefix);
        private static readonly string EndpointSuffix = "/?v=4";
        private static readonly byte[] EndpointSuffixU8 =
            Encoding.UTF8.GetBytes(EndpointSuffix);

        private readonly ConcurrentDictionary<ulong, IPlayer> _players;
        private readonly ImmutableDictionary<int, SocketInfo> _udpClients;

        private State _state;

        /// <summary>
        /// Fired when a message is logged by a client.
        /// </summary>
        public event EventHandler<LogMessage>? Log;

        /// <summary>
        /// The user ID to perform all connections as.
        /// </summary>
        public ulong UserId { get; }

        //public IReadOnlyList<ITrackSource> Sources { get; }

        /// <summary>
        /// Creates a new instance of the <see cref="PlayerManager"/> type.
        /// </summary>
        /// <param name="userId">The user ID to connect as.</param>
        /// <param name="clientCount">The number of sockets to create.</param>
        public PlayerManager(ulong userId, int clientCount = 20)
        {
            UserId = userId;

            _players = new ConcurrentDictionary<ulong, IPlayer>();

            var builder = ImmutableDictionary.CreateBuilder<int, SocketInfo>();
            for (int x = 0; x < clientCount; x++)
            {
                builder.Add(x, new SocketInfo());
            }

            _udpClients = builder.ToImmutable();

            _state.Transition(to: Initialized);
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            if (_state.BeginDispose() < Disposing)
            {
                foreach (var pair in _players)
                {
                    var player = (Player)pair.Value;

                    await player.DisposeAsync()
                        .ConfigureAwait(false);
                }

                foreach (var pair in _udpClients)
                {
                    pair.Value.Socket.Dispose();
                }

                _state.EndDispose();
            }
        }

        /// <summary>
        /// Updates the voice state of the player manager in a specific guild.
        /// </summary>
        /// <param name="guildId">The guild ID this update is for.</param>
        /// <param name="sessionId">The session ID to connect using.</param>
        /// <param name="endpoint">The endpoint to connect to.</param>
        /// <param name="token">The token to connect with.</param>
        public void UpdateVoiceState(ulong guildId, string sessionId,
            string endpoint, string token)
            => UpdateVoiceState(guildId, sessionId.AsSpan(), endpoint.AsSpan(),
                token.AsSpan());


        /// <summary>
        /// Updates the voice state of the player manager in a specific guild.
        /// </summary>
        /// <param name="guildId">The guild ID this update is for.</param>
        /// <param name="sessionId">The session ID to connect using.</param>
        /// <param name="endpoint">The endpoint to connect to.</param>
        /// <param name="token">The token to connect with.</param>
        public void UpdateVoiceState(ulong guildId,
            ReadOnlySpan<char> sessionId, ReadOnlySpan<char> endpoint,
            ReadOnlySpan<char> token)
        {
            var player = _players.GetOrAdd(guildId, CreatePlayer,
                new PlayerInfo
                {
                    UserInfo = new UserToken(UserId, guildId, sessionId,
                        token),
                    EndPoint = CreateUri(endpoint),
                    SocketInfo = GetSocketInfo(guildId),
                });
        }

        /// <summary>
        /// Updates the voice state of the player manager in a specific guild.
        /// </summary>
        /// <param name="guildId">The guild ID this update is for.</param>
        /// <param name="sessionId">The session ID to connect using.</param>
        /// <param name="endpoint">The endpoint to connect to.</param>
        /// <param name="token">The token to connect with.</param>
        public void UpdateVoiceState(ulong guildId,
            ReadOnlySpan<byte> sessionId, ReadOnlySpan<byte> endpoint,
            ReadOnlySpan<byte> token)
        {
            var player = _players.GetOrAdd(guildId, CreatePlayer,
                new PlayerInfo
                {
                    UserInfo = new UserToken(UserId, guildId, sessionId,
                        token),
                    EndPoint = CreateUri(endpoint),
                    SocketInfo = GetSocketInfo(guildId),
                });
        }

        /// <summary>
        /// Attempts to get the <see cref="IPlayer"/> for the specified
        /// <paramref name="guildId"/>.
        /// </summary>
        /// <param name="guildId">The guild ID to get the player for.</param>
        /// <param name="player">The player, if it exists.</param>
        /// <returns>
        /// <code>true</code> if successful, <code>false</code> otherwise.
        /// </returns>
        public bool TryGetPlayer(ulong guildId, out IPlayer? player)
            => _players.TryGetValue(guildId, out player);

        /// <summary>
        /// Gets the <see cref="IPlayer"/> for the specified
        /// <paramref name="guildId"/>.
        /// </summary>
        /// <param name="guildId">The guild ID to get the player for.</param>
        /// <returns>the found <see cref="IPlayer"/> if successful.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the player does not exist because it is not connected.
        /// </exception>
        public IPlayer GetPlayer(ulong guildId)
        {
            if (_players.TryGetValue(guildId, out var player))
                return player;

            throw new InvalidOperationException("Connect to voice first");
        }

        private IPlayer CreatePlayer(ulong guildId, PlayerInfo info)
        {
            var player = new Player(info.UserInfo, info.SocketInfo.Socket,
                info.EndPoint, info.SocketInfo.DiscoveryEndPoint);

            player.ClientEndPointUpdated +=
                (_, endPoint) =>
                {
                    info.SocketInfo.DiscoveryEndPoint = endPoint;
                };

            player.Log += (e, message) =>
            {
                Log?.Invoke(e, message);
            };

            player.Start();

            return player;
        }

        private static Uri CreateUri(ReadOnlySpan<char> endpoint)
        {
            var portIndicator = endpoint.IndexOf(':');
            if (portIndicator > 0)
                endpoint = endpoint.Slice(0, portIndicator);

            Span<char> buffer = stackalloc char[EndpointPrefix.Length
                + endpoint.Length + EndpointSuffix.Length];

            EndpointPrefix.AsSpan().CopyTo(buffer);
            endpoint.CopyTo(buffer.Slice(EndpointPrefix.Length));
            EndpointSuffix.AsSpan().CopyTo(buffer.Slice(
                EndpointPrefix.Length + endpoint.Length));

            return new Uri(new string(buffer));
        }

        private static Uri CreateUri(ReadOnlySpan<byte> endpoint)
        {
            var portIndicator = endpoint.IndexOf((byte)':');
            if (portIndicator > 0)
                endpoint = endpoint.Slice(0, portIndicator);

            Span<byte> buffer = stackalloc byte[EndpointPrefixU8.Length
                + endpoint.Length + EndpointSuffixU8.Length];

            EndpointPrefixU8.AsSpan().CopyTo(buffer);
            endpoint.CopyTo(buffer.Slice(EndpointPrefixU8.Length));
            EndpointSuffixU8.AsSpan().CopyTo(buffer.Slice(
                EndpointPrefixU8.Length + endpoint.Length));

            return new Uri(Encoding.UTF8.GetString(buffer));
        }

        private SocketInfo GetSocketInfo(ulong guildId)
            => _udpClients[unchecked(
                (int)(guildId >> 22) % _udpClients.Count)];

        private struct PlayerInfo
        {
            public UserToken UserInfo;
            public Uri EndPoint;
            public SocketInfo SocketInfo;
        }

        private class SocketInfo
        {
            public readonly Socket Socket;
            public IPEndPoint? DiscoveryEndPoint;

            public SocketInfo()
            {
                Socket = new Socket(AddressFamily.InterNetwork,
                    SocketType.Dgram, ProtocolType.Udp);
            }
        }
    }
}