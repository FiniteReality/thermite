using System;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace Thermite.Core
{
    /// <summary>
    /// Represents a player for a specific guild
    /// </summary>
    public interface IPlayer
    {
        //// <summary>
        //// Enqueues the given track for playback
        //// </summary>
        //// <param name="url">The url of the track</param>
        //// <returns>A task representing the asynchronous completion</returns>
        //Task EnqueueAsync(Uri url);

        /// <summary>
        /// meme
        /// </summary>
        PipeWriter Writer { get; }
    }
}