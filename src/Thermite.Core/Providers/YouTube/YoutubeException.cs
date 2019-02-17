using System;

namespace Thermite.Core.Providers
{
    public class YoutubeException : Exception
    {
        public YoutubeException(string message)
            : base(message)
        { }
    }
}