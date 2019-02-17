using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Thermite.Core
{
    public interface IAudioSource
    {
        Task<IReadOnlyCollection<IAudioFile>> GetTracksAsync(Uri url);
    }
}