using System;

namespace Thermite.Utilities
{
    /// <summary>
    /// Defines the type of a member as it was used in the native signature.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Enum |
        AttributeTargets.Property |
        AttributeTargets.Field |
        AttributeTargets.Parameter |
        AttributeTargets.ReturnValue,
        AllowMultiple = false,
        Inherited = true)]
    internal sealed class NativeTypeNameAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NativeTypeNameAttribute" /> class.
        /// </summary>
        /// <param name="name">
        /// The name of the type that was used in the native signature.
        /// </param>
        public NativeTypeNameAttribute(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Gets the name of the type that was used in the native signature.
        /// </summary>
        public string Name { get; }
    }
}
