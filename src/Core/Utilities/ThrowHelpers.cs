using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Thermite.Utilities
{
    /// <summary>
    /// Provides a set of methods for throwing exceptions.
    /// </summary>
    public static class ThrowHelpers
    {
        /// <summary>
        /// Throws an instance of the <see cref="ArgumentException"/> class.
        /// </summary>
        /// <param name="paramName">
        /// The name of the parameter that caused the exception.
        /// </param>
        /// <param name="message">
        /// The message of the exception.
        /// </param>
        /// <exception cref="ArgumentException">
        /// The parameter <paramref name="paramName"/> was invalid because of
        /// <paramref name="message"/>.
        /// </exception>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentException(string? paramName,
            string? message)
            => throw new ArgumentException(message, paramName);

        /// <summary>
        /// Throws an instance of the <see cref="ArgumentOutOfRangeException"/>.
        /// </summary>
        /// <param name="paramName">
        /// The parameter that is out of valid range.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The parameter <paramref name="paramName"/> was outside of the valid
        /// range.
        /// </exception>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentOutOfRangeException(string? paramName)
            => throw new ArgumentOutOfRangeException(paramName);

        /// <summary>
        /// Throws an instance of the <see cref="ArgumentOutOfRangeException"/>.
        /// </summary>
        /// <param name="paramName">
        /// The parameter that is out of valid range.
        /// </param>
        /// <param name="actualValue">
        /// The actual value of <paramref name="paramName"/>.
        /// </param>
        /// <param name="message">
        /// The message of the exception.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The parameter <paramref name="paramName"/> was outside of the valid
        /// range due to <paramref name="message"/>.
        /// </exception>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentOutOfRangeException(string? paramName,
            object? actualValue,
            string? message)
        {
            throw new ArgumentOutOfRangeException(
                paramName, actualValue, message);
        }

        /// <summary>
        /// Throws an instance of the <see cref="ExternalException"/> class.
        /// </summary>
        /// <param name="message">
        /// The message of the exception.
        /// </param>
        /// <param name="errorCode">
        /// The error code that caused the exception.
        /// </param>
        /// <exception cref="ExternalException">
        /// A native call returned <paramref name="errorCode"/> due to
        /// <paramref name="message"/>.
        /// </exception>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowExternalException(string? message,
            int errorCode)
            => throw new ExternalException(message, errorCode);

        /// <summary>
        /// Throws an instance of the <see cref="InvalidOperationException"/>
        /// class.
        /// </summary>
        /// <param name="message">
        /// The message of the exception.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// The operation attempted was invalid due to
        /// <paramref name="message"/>.
        /// </exception>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowInvalidOperationException(string? message)
            => throw new InvalidOperationException(message);

        /// <summary>
        /// Throws an instance of the <see cref="InvalidUriException"/> class.
        /// </summary>
        /// <param name="paramName">
        /// The name of the parameter that caused the exception.
        /// </param>
        /// <param name="location">
        /// The <see cref="Uri"/> that was invalid.
        /// </param>
        /// <exception cref="InvalidUriException">
        /// The parameter <paramref name="paramName"/> referred to an
        /// invalid resource <paramref name="location"/>.
        /// </exception>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowInvalidUriException(string? paramName,
            Uri location)
            => throw new InvalidUriException(paramName, location);

        /// <summary>
        /// Throws an instance of the <see cref="ObjectDisposedException"/>
        /// class.
        /// </summary>
        /// <param name="objectName">
        /// The name of the object that was disposed.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// The object <paramref name="objectName"/> was disposed.
        /// </exception>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowObjectDisposedException(string? objectName)
            => throw new ObjectDisposedException(objectName);

        /// <summary>
        /// Throws an instance of the
        /// <see cref="PlatformNotSupportedException"/> class.
        /// </summary>
        /// <param name="message">
        /// The message of the exception.
        /// </param>
        /// <exception cref="PlatformNotSupportedException">
        /// The current platform is not supported due to
        /// <paramref name="message"/>.
        /// </exception>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowPlatformNotSupportedException(string? message)
            => throw new PlatformNotSupportedException(message);
    }
}
