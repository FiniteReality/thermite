using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Thermite.Core.Streams;

namespace Thermite.Core
{
    public interface IAudioFile
    {
        Uri Location { get; }
        IReadOnlyDictionary<string, string> Metadata { get; }

        Task<ThermiteStream> GetStreamAsync();
    }
}