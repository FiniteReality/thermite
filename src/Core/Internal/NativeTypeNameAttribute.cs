using System;

namespace Thermite.Utilities
{
    [AttributeUsage(
        AttributeTargets.Struct |
        AttributeTargets.ReturnValue |
        AttributeTargets.Parameter,
        AllowMultiple = false,
        Inherited = false)]
    internal class NativeTypeNameAttribute : Attribute
    {
        public string NativeTypeName { get; }

        public NativeTypeNameAttribute(string name)
        {
            NativeTypeName = name;
        }
    }
}
