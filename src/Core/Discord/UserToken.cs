using System;
using System.Buffers;
using System.Text;

namespace Thermite.Discord
{
    internal class UserToken : IDisposable
    {
        public ulong UserId { get; }
        public ulong GuildId { get; }

        public ReadOnlySpan<byte> SessionId
            => _sessionIdMemory.Memory.Span.Slice(0, _sessionIdLength);
        public ReadOnlySpan<byte> Token
            => _tokenMemory.Memory.Span.Slice(0, _tokenLength);

        private readonly IMemoryOwner<byte> _sessionIdMemory;
        private readonly int _sessionIdLength;
        private readonly IMemoryOwner<byte> _tokenMemory;
        private readonly int _tokenLength;

        public UserToken(ulong userId, ulong guildId,
            ReadOnlySpan<char> sessionId, ReadOnlySpan<char> token,
            MemoryPool<byte>? memoryPool = default)
        {
            UserId = userId;
            GuildId = guildId;

            memoryPool ??= MemoryPool<byte>.Shared;

            _sessionIdLength = Encoding.UTF8.GetByteCount(sessionId);
            _sessionIdMemory = memoryPool.Rent(_sessionIdLength);
            Encoding.UTF8.GetBytes(sessionId,
                _sessionIdMemory.Memory.Span);

            _tokenLength = Encoding.UTF8.GetByteCount(token);
            _tokenMemory = memoryPool.Rent(_tokenLength);
            Encoding.UTF8.GetBytes(token,
                _tokenMemory.Memory.Span);
        }

        public UserToken(ulong userId, ulong guildId,
            ReadOnlySpan<byte> sessionId, ReadOnlySpan<byte> token,
            MemoryPool<byte>? memoryPool = default)
        {
            UserId = userId;
            GuildId = guildId;

            memoryPool ??= MemoryPool<byte>.Shared;

            _sessionIdLength = sessionId.Length;
            _sessionIdMemory = memoryPool.Rent(_sessionIdLength);
            sessionId.CopyTo(_sessionIdMemory.Memory.Span);

            _tokenLength = token.Length;
            _tokenMemory = memoryPool.Rent(_tokenLength);
            token.CopyTo(_tokenMemory.Memory.Span);
        }

        public void Dispose()
        {
            _sessionIdMemory.Dispose();
            _tokenMemory.Dispose();
        }
    }
}