using System;
using System.Threading;
using System.Threading.Tasks;

namespace Thermite.Core.Streams
{
    public abstract class ThermiteStream : IDisposable
    {
        protected bool _disposed = false;

        ~ThermiteStream()
        {
            Dispose(false);
        }

        public abstract Task<ReadResult> ReadAsync(ArraySegment<byte> array, CancellationToken cancelToken);

        protected virtual void Dispose(bool disposing)
        { }

        public void Dispose()
        {
            if (!_disposed)
            {
                Dispose(true);
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        public struct ReadResult
        {
            public ReadResult(int bytesRead, TimeSpan length, uint samples)
            {
                BytesRead = bytesRead;
                FrameLength = length;
                SampleCount = samples;
            }

            public int BytesRead { get; }
            public TimeSpan FrameLength { get; }
            public uint SampleCount { get; }
        }
    }
}