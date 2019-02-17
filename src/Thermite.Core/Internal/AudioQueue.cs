using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Thermite.Core
{
    internal class AudioQueue : IAudioQueue, IReadOnlyCollection<IAudioFile>
    {
        private BlockingCollection<IAudioFile> _entries
            = new BlockingCollection<IAudioFile>();

        public int Count => _entries.Count;

        public void Finish()
            => _entries.CompleteAdding();

        void IAudioQueue.AddAudioFile(IAudioFile file)
            => _entries.Add(file);

        public IEnumerator<IAudioFile> GetEnumerator()
            => _entries.GetConsumingEnumerable().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();
    }
}