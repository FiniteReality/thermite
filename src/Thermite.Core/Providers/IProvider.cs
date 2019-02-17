using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Thermite.Core.Streams;

namespace Thermite.Core.Providers
{
    public interface IProvider
    {
        bool IsSupported(Uri url);

        Task<IReadOnlyCollection<TrackInfo>> GetTracksAsync(Uri url);
    }
}