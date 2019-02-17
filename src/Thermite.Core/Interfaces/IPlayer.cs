using System;
using System.Threading.Tasks;

namespace Thermite.Core
{
    public interface IPlayer
    {
        PlayerState State { get; }

        IAudioFile CurrentFile { get; }

        IAudioQueue Queue { get; }

        Task PausePlaybackAsync();
        Task ResumePlaybackAsync();
    }
}