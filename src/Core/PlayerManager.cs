using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TerraFX.Utilities;
using Thermite.Discord;
using Thermite.Internal;

using static TerraFX.Utilities.State;
using static Thermite.Utilities.ThrowHelpers;

namespace Thermite
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

        private readonly ILogger _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ConcurrentDictionary<ulong, IPlayer> _players;
        private readonly ImmutableDictionary<int, SocketInfo> _udpClients;

        private State _state;

        /// <summary>
        /// The user ID to perform all connections as.
        /// </summary>
        public ulong UserId { get; }

        /// <summary>
        /// Gets a list of decoder factories which can be used to retrieve
        /// audio frames from audio files.
        /// </summary>
        public IReadOnlyList<IAudioDecoderFactory> Decoders { get; }

        /// <summary>
        /// Gets a list of provider factories which can be used to retrieve
        /// audio files from tracks.
        /// </summary>
        public IReadOnlyList<IAudioProviderFactory> Providers { get; }

        /// <summary>
        /// Gets a list of sources which can be used to retrieve tracks
        /// </summary>
        public IReadOnlyList<ITrackSource> Sources { get; }

        /// <summary>
        /// Gets a list of transcoder factories which can be used to retrieve
        /// Opus packets from audio files.
        /// </summary>
        public IReadOnlyList<IAudioTranscoderFactory> Transcoders { get; }

        /// <summary>
        /// An event which is thrown when a player task throws an exception.
        /// </summary>
        public event EventHandler<UnobservedTaskExceptionEventArgs>?
            PlayerProcessingException;

        internal PlayerManager(ulong userId, uint socketCount,
            ILoggerFactory loggerFactory,
            IReadOnlyList<IAudioDecoderFactory> decoders,
            IReadOnlyList<IAudioProviderFactory> providers,
            IReadOnlyList<ITrackSource> sources,
            IReadOnlyList<IAudioTranscoderFactory> transcoders)
        {
            UserId = userId;
            Providers = providers;
            Sources = sources;
            Decoders = decoders;
            Transcoders = transcoders;

            _logger = loggerFactory.CreateLogger<PlayerManager>();
            _loggerFactory = loggerFactory;
            _players = new ConcurrentDictionary<ulong, IPlayer>();

            var builder = ImmutableDictionary.CreateBuilder<int, SocketInfo>();
            for (int x = 0; x < socketCount; x++)
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

                    await player.DisposeAsync();
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
            _logger.LogDebug("Updating voice state for {guildId}", guildId);

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
            _logger.LogDebug("Updating voice state for {guildId}", guildId);

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

            ThrowInvalidOperationException("Connect to voice first");

            return null; // not hit, above does not return
        }

        private IPlayer CreatePlayer(ulong guildId, PlayerInfo info)
        {
            var logger = _loggerFactory.CreateLogger<Player>();
            var player = new Player(this, logger, info.UserInfo,
                info.SocketInfo.Socket, info.EndPoint,
                info.SocketInfo.DiscoveryEndPoint);

            player.ClientEndPointUpdated +=
                (_, endPoint) => {
                    _logger.LogTrace(
                        "Socket endpoint for {guildId} updated to {endpoint}",
                        guildId, endPoint);
                    info.SocketInfo.DiscoveryEndPoint = endPoint;
                };

            player.ProcessingException +=
                (sender, args) => {
                    PlayerProcessingException?.Invoke(sender, args);
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

        internal bool TryGetProviderFactory(Uri location,
            [NotNullWhen(true)]out IAudioProviderFactory? factory)
        {
            foreach (var providerFactory in Providers)
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

        internal bool TryGetTranscoderFactory(IAudioCodec codec,
            [NotNullWhen(true)]out IAudioTranscoderFactory? factory)
        {
            foreach (var transcoderFactory in Transcoders)
            {
                if (transcoderFactory.IsSupported(codec))
                {
                    factory = transcoderFactory;
                    return true;
                }
            }

            factory = default;
            return false;
        }

        internal bool TryGetDecoderFactory(string mediaType,
            [NotNullWhen(true)]out IAudioDecoderFactory? factory)
        {
            foreach (var decoderFactory in Decoders)
            {
                if (decoderFactory.IsSupported(mediaType))
                {
                    factory = decoderFactory;
                    return true;
                }
            }

            factory = default;
            return false;
        }

        private struct PlayerInfo
        {
            public UserToken UserInfo;
            public Uri EndPoint;
            public SocketInfo SocketInfo;
        }

        private class SocketInfo
        {
            private readonly Lazy<Socket> _socket;
            public IPEndPoint? DiscoveryEndPoint;

            public Socket Socket => _socket.Value;

            public SocketInfo()
            {
                _socket = new Lazy<Socket>(CreateSocket, true);

                static Socket CreateSocket()
                {
                    return new Socket(AddressFamily.InterNetwork,
                        SocketType.Dgram, ProtocolType.Udp);
                }
            }
        }
    }
}
