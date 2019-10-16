using System;

using static Thermite.Utilities.ThrowHelpers;
using static System.Threading.Interlocked;

namespace Thermite.Utilities
{
    internal struct State
    {
        public const int Uninitialized = 0;
        public const int Initialized = 1;
        public const int Disposing = int.MaxValue - 1;
        public const int Disposed = int.MaxValue;

        private volatile int _value;

        public static implicit operator int(State state)
            => state._value;

        public int BeginDispose()
            => Transition(to: Disposing);

        public void EndDispose()
        {
            var previous = TryTransition(from: Disposing, to: Disposed);
            if (previous != Disposing)
                ThrowInvalidOperationException(
                    $"Tried to end a dispose block when the state wasn't {nameof(Disposing)}");
        }

        public int Transition(int to)
            => Exchange(ref _value, to);

        public int TryTransition(int from, int to)
            => CompareExchange(ref _value, to, from);

        public void ThrowIfDisposed(string? objectName)
        {
            if (_value > Disposing)
                throw new ObjectDisposedException(objectName);
        }
    }
}