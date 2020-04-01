namespace Thermite
{
    /// <summary>
    /// An enum describing potential sample formats.
    /// </summary>
    public enum SampleFormat
    {
        /// <summary>
        /// Each sample in the data stream is a signed integer.
        /// </summary>
        SignedInteger,
        /// <summary>
        /// Each sample in the data stream is an unsigned integer.
        /// </summary>
        UnsignedInteger,

        /// <summary>
        /// Each sample in the data stream is a fixed-point number.
        /// </summary>
        FixedPoint,

        /// <summary>
        /// Each sample in the data stream is a floating-point number.
        /// </summary>
        FloatingPoint
    }
}
