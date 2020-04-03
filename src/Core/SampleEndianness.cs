namespace Thermite
{
    /// <summary>
    /// An enum describing potential sample endiannesses.
    /// </summary>
    public enum SampleEndianness
    {
        /// <summary>
        /// Each sample in the data stream is neither big endian nor little
        /// endian.
        /// </summary>
        Indeterminate,

        /// <summary>
        /// Each sample in the data stream uses big-endian byte order.
        /// </summary>
        BigEndian,

        /// <summary>
        /// Each sample in the data stream uses little-endian byte order.
        /// </summary>
        LittleEndian,
    }
}
