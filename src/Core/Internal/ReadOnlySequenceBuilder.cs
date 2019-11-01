using System;
using System.Buffers;
using System.Diagnostics;

namespace Thermite.Internal
{
    [DebuggerStepThrough]
    internal class ReadOnlySequenceBuilder<T> : IDisposable
    {
        private const int BlockSize = 1 << 16;

        private readonly MemoryPool<T> _pool;

        private readonly Segment _first;
        private Segment _head;

        [DebuggerStepThrough]
        public ReadOnlySequenceBuilder(MemoryPool<T>? pool = default)
        {
            _pool = pool ?? MemoryPool<T>.Shared;
            _first = new Segment(null, _pool.Rent(BlockSize));
            _head = _first;
        }

        [DebuggerStepThrough]
        public Memory<T> GetMemory(int minLength = 0)
        {
            Memory<T> result;
            do
            {
                result = _head._memory.Memory.Slice(_head.Length);

                if (result.Length < minLength || result.Length == 0)
                    _head = CreateNewHead();

            } while (result.Length < minLength || result.Length == 0);

            return result;
        }

        [DebuggerStepThrough]
        public void Advance(int bytesWritten)
        {
            _head!.Length += bytesWritten;

            if (_head.Length == BlockSize)
                _head = CreateNewHead();
        }

        [DebuggerStepThrough]
        public ReadOnlySequence<T> AsSequence()
        {
            return new ReadOnlySequence<T>(_first!, 0, _head!, _head!.Length);
        }

        [DebuggerStepThrough]
        public void Dispose()
        {
            _first!.Dispose();
        }

        [DebuggerStepThrough]
        private Segment CreateNewHead()
            => new Segment(_head, _pool.Rent(BlockSize));

        [DebuggerStepThrough]
        private class Segment : ReadOnlySequenceSegment<T>, IDisposable
        {
            public readonly IMemoryOwner<T> _memory;

            private int _length;

            public int Length
            {
                get => _length;
                set
                {
                    _length = value;
                    Memory = _memory.Memory.Slice(0, value);
                }
            }

            [DebuggerStepThrough]
            public Segment(Segment? previous, IMemoryOwner<T> memory)
            {
                if (previous != null)
                    previous.Next = this;

                _memory = memory;
                Length = 0;
                RunningIndex = (previous?.RunningIndex ?? 0) +
                    (previous?.Length ?? 0);
            }

            [DebuggerStepThrough]
            public void Dispose()
            {
                _memory.Dispose();

                if (Next != null)
                    ((Segment)Next).Dispose();
            }
        }
    }
}
