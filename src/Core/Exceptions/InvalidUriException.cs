using System;

namespace Thermite
{
    /// <summary>
    /// An exception thrown when a given <see cref="Uri"/> is not valid.
    /// </summary>
    public class InvalidUriException : ArgumentOutOfRangeException
    {
        /// <summary>
        /// Constructs a new instance of the <see cref="InvalidUriException"/>
        /// type.
        /// </summary>
        /// <param name="paramName">
        /// The name of the parameter which was out of the acceptable range.
        /// </param>
        /// <param name="value">
        /// The <see cref="Uri"/> which caused the exception.
        /// </param>
        public InvalidUriException(string? paramName, Uri value)
            : base(paramName, value, GetMessage())
        {
        }

        private static string GetMessage()
            => "The given Uri was not supported or invalid.";
    }
}
