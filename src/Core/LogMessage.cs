namespace Thermite
{
    /// <summary>
    /// Represents a log message to various log events.
    /// </summary>
    public struct LogMessage
    {
        /// <summary>
        /// The log category
        /// </summary>
        public readonly string Category;

        /// <summary>
        /// The message to log
        /// </summary>
        public readonly string Message;

        /// <summary>
        /// Creates a new instance of the <see cref="LogMessage" /> type.
        /// </summary>
        /// <param name="category">The category of the log message</param>
        /// <param name="message">The message to log</param>
        public LogMessage(string category, string message)
        {
            Category = category;
            Message = message;
        }
    }
}