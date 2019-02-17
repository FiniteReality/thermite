using System;

namespace Thermite.Core.Caching
{
    public struct CacheEntryOptions
    {
        public DateTimeOffset? AbsoluteExpiration { get; set; }
        public TimeSpan? SlidingExpiration { get; set; }

        public static CacheEntryOptions WithAbsoluteExpiration(
            DateTimeOffset expiration)
        {
            return new CacheEntryOptions
            {
                AbsoluteExpiration = expiration
                };
        }

        public static CacheEntryOptions WithAbsoluteExpirationFromNow(
            TimeSpan expiration)
        {
            return new CacheEntryOptions
            {
                AbsoluteExpiration = DateTimeOffset.Now.Add(expiration)
            };
        }

        public static CacheEntryOptions WithSlidingExpiration(
            TimeSpan expiration)
        {
            return new CacheEntryOptions
            {
                SlidingExpiration = expiration
            };
        }
    }
}