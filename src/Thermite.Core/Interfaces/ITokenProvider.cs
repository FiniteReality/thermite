using System.Threading.Tasks;
using Wumpus;

namespace Thermite.Core
{
    public interface ITokenProvider
    {
        Task<TokenInfo> GetTokenAsync(Snowflake userId, Snowflake guildId,
            Snowflake channelId);
    }
}