using System;
using Voltaic;

namespace Thermite.Core
{
    internal struct AudioFrame
    {
        public TimeSpan Length { get; set; }
        public uint Samples { get; set; }
        public ResizableMemory<byte> Data { get; set; }
    }
}