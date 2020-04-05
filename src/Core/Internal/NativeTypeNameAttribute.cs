using System;

namespace Thermite.Internal
{
    [AttributeUsage(
        AttributeTargets.Field |
        AttributeTargets.Parameter |
        AttributeTargets.ReturnValue |
        AttributeTargets.Struct,
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
