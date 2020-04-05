using System;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using static Thermite.Internal.ThrowHelpers;

namespace Thermite
{
    /// <summary>
    /// A builder for creating instances of <see cref="PlayerManager"/>.
    /// </summary>
    public class PlayerManagerBuilder
    {
        private readonly ulong _userId;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ImmutableList<IAudioDecoderFactory>.Builder _decoders;
        private readonly ImmutableList<IAudioProviderFactory>.Builder
            _providers;
        private readonly ImmutableList<ITrackSource>.Builder _sources;
        private readonly ImmutableList<IAudioTranscoderFactory>.Builder
            _transcoders;

        private uint _socketCount;

        /// <summary>
        /// Creates a new instance of the <see cref="PlayerManagerBuilder"/>
        /// type.
        /// </summary>
        /// <param name="userId">
        /// The user ID to perform all connections as.
        /// </param>
        /// <param name="services">
        /// The service provider to locate any existing services from.
        /// </param>
        public PlayerManagerBuilder(ulong userId, IServiceProvider services)
        {
            _userId = userId;
            _loggerFactory = services.GetRequiredService<ILoggerFactory>();
            _decoders = ImmutableList.CreateBuilder<IAudioDecoderFactory>();
            _providers = ImmutableList.CreateBuilder<IAudioProviderFactory>();
            _sources = ImmutableList.CreateBuilder<ITrackSource>();
            _transcoders = ImmutableList
                .CreateBuilder<IAudioTranscoderFactory>();

            _socketCount = 20;

            _decoders.AddRange(
                services.GetServices<IAudioDecoderFactory>());
            _providers.AddRange(
                services.GetServices<IAudioProviderFactory>());
            _sources.AddRange(
                services.GetServices<ITrackSource>());
            _transcoders.AddRange(
                services.GetServices<IAudioTranscoderFactory>());
        }

        /// <summary>
        /// Creates a new instance of the <see cref="PlayerManagerBuilder"/>
        /// type.
        /// </summary>
        /// <param name="userId">
        /// The user ID to perform all connections as.
        /// </param>
        /// <param name="loggerFactory">
        /// The logger factory to use for any logging events which may occur.
        /// </param>
        public PlayerManagerBuilder(ulong userId, ILoggerFactory loggerFactory)
        {
            _userId = userId;
            _loggerFactory = loggerFactory;
            _decoders = ImmutableList.CreateBuilder<IAudioDecoderFactory>();
            _providers = ImmutableList.CreateBuilder<IAudioProviderFactory>();
            _sources = ImmutableList.CreateBuilder<ITrackSource>();
            _transcoders = ImmutableList
                .CreateBuilder<IAudioTranscoderFactory>();

            _socketCount = 20;
        }

        /// <summary>
        /// Overrides the default UDP socket count used for transmitting audio.
        /// </summary>
        /// <param name="socketCount">The number of UDP sockets to use.</param>
        /// <returns><code>this</code></returns>
        public PlayerManagerBuilder WithSocketCount(uint socketCount)
        {
            if (socketCount == 0)
                ThrowArgumentOutOfRangeException(nameof(socketCount));

            _socketCount = socketCount;

            return this;
        }

        /// <summary>
        /// Adds an <see cref="IAudioDecoderFactory"/> used for decoding audio
        /// data.
        /// </summary>
        /// <param name="decoder">The decoder to add.</param>
        /// <returns><code>this</code></returns>
        public PlayerManagerBuilder AddDecoder(IAudioDecoderFactory decoder)
        {
            _decoders.Add(decoder);

            return this;
        }

        /// <summary>
        /// Adds an <see cref="IAudioProviderFactory"/> used for providing
        /// audio data.
        /// </summary>
        /// <param name="provider">The provider to add.</param>
        /// <returns><code>this</code></returns>
        public PlayerManagerBuilder AddProvider(IAudioProviderFactory provider)
        {
            _providers.Add(provider);

            return this;
        }

        /// <summary>
        /// Adds an <see cref="ITrackSource"/> used for providing track
        /// information.
        /// </summary>
        /// <param name="source">The track source to add.</param>
        /// <returns><code>this</code></returns>
        public PlayerManagerBuilder AddSource(ITrackSource source)
        {
            _sources.Add(source);

            return this;
        }

        /// <summary>
        /// Adds an <see cref="IAudioTranscoderFactory"/> used for transcoding
        /// audio data to Thermite-compatible Opus packets.
        /// </summary>
        /// <param name="transcoder">The transcoder to add.</param>
        /// <returns><code>this</code></returns>
        public PlayerManagerBuilder AddTranscoder(
            IAudioTranscoderFactory transcoder)
        {
            _transcoders.Add(transcoder);

            return this;
        }

        /// <summary>
        /// Builds a <see cref="PlayerManager"/> with the configured
        /// properties.
        /// </summary>
        /// <returns>A configured <see cref="PlayerManager"/>.</returns>
        public PlayerManager Build()
        {
            return new PlayerManager(_userId, _socketCount,
                _loggerFactory,
                _decoders.ToImmutable(),
                _providers.ToImmutable(),
                _sources.ToImmutable(),
                _transcoders.ToImmutable());
        }
    }
}
