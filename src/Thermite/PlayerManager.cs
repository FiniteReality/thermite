using System;

namespace Thermite.Core
{
    /// <summary>
    /// Manages instances of <see cref="IPlayer" />.
    /// </summary>
    public class PlayerManager
    {
        /// <summary>
        /// The user ID to perform all connections as
        /// </summary>
        public ulong UserId { get; }

        /// <summary>
        /// Creates a new instance of the <see cref="PlayerManager"/> type.
        /// </summary>
        /// <param name="userId">The user ID to perform all connections as</param>
        public PlayerManager(ulong userId)
        {
            UserId = userId;
        }

        /// <summary>
        /// Updates the voice state of the player manager in a specific guild.
        /// </summary>
        /// <param name="guildId">The guild ID this update is for.</param>
        /// <param name="sessionId">The session ID to connect using.</param>
        /// <param name="endpoint">The endpoint to connect to.</param>
        /// <param name="token">The token to connect with.</param>
        public void UpdateVoiceState(ulong guildId, Span<char> sessionId,
            Span<char> endpoint, Span<char> token)
        {

        }

        /// <summary>
        /// Updates the voice state of the player manager in a specific guild.
        /// </summary>
        /// <param name="guildId">The guild ID this update is for.</param>
        /// <param name="sessionId">The session ID to connect using.</param>
        /// <param name="endpoint">The endpoint to connect to.</param>
        /// <param name="token">The token to connect with.</param>
        public void UpdateVoiceState(ulong guildId, Span<byte> sessionId,
            Span<byte> endpoint, Span<byte> token)
        {

        }
    }
}