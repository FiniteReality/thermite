using System.Threading.Tasks;

namespace Thermite.Core.Caching
{
    public interface ICache
    {
        Task<T> GetAsync<T>(string key);
        Task SetAsync<T>(string key, T value, CacheEntryOptions options = default);
        Task RefreshAsync(string key);
        Task RemoveAsync(string key);
    }
}