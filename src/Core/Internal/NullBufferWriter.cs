using System;
using System.Buffers;
using System.Diagnostics;

namespace Thermite.Internal
{
    /// <summary>
    /// A null-pipe buffer writer which does nothing.
    /// </summary>
    /// <remarks>
    /// If this type is used for writing, it will return empty destinations to
    /// write, and in debug mode will call <see cref="Debug.Fail(string?)"/>.
    /// </remarks>
    /// <typeparam name="T">
    /// The type of data to write.
    /// </typeparam>
    internal class NullBufferWriter<T> : IBufferWriter<T>
    {
        public static NullBufferWriter<T> Instance { get; }
            = new NullBufferWriter<T>();

        private NullBufferWriter()
        { }

        public void Advance(int count)
            => Debug.Fail(
                $"{nameof(NullBufferWriter<T>)} attempted to advance");

        public Memory<T> GetMemory(int sizeHint = 0)
        {
            Debug.Fail(
                $"{nameof(NullBufferWriter<T>)} attempted to get memory");

            return default;
        }

        public Span<T> GetSpan(int sizeHint = 0)
        {
            Debug.Fail(
                $"{nameof(NullBufferWriter<T>)} attempted to get a span");

            return default;
        }
    }
}
