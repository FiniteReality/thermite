using System;
using System.Runtime.InteropServices;

namespace Thermite.Core.Natives
{
    // TODO: refactor and split this into more appropriate files

    internal unsafe class Opus
    {
        [DllImport("opus", EntryPoint = "opus_packet_get_samples_per_frame",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int SamplesPerFrame(byte* samples,
            int sampleRate);

        [DllImport("opus", EntryPoint="opus_encoder_create",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern void* CreateEncoder(int sampleRate, int channels,
            int application, int* error);

        [DllImport("opus", EntryPoint = "opus_encoder_destroy",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern void DestroyEncoder(void* encoder);

        [DllImport("opus", EntryPoint = "opus_encode",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Encode(void* state, byte* pcm,
            int frameSize, byte* data, int dataLength);

        [DllImport("opus", EntryPoint = "opus_encoder_ctl")]
        internal static extern int EncoderCtl(void* state, int request,
            int value);

        public static int GetSamplesPerFrame(
            ReadOnlySpan<byte> packet, int sampleRate)
        {
            fixed(byte* packetData = &packet.GetPinnableReference())
                return SamplesPerFrame(packetData, sampleRate);
        }

        public static OpusEncoder CreateEncoder(int sampleRate, int channels,
            Application application)
        {
            int error = 0;
            var result = CreateEncoder(sampleRate, channels,
                (int)application, &error);

            if (error != 0)
                throw new Exception($"Failed to create Opus encoder: {error}");

            return new OpusEncoder(result);
        }

        internal static int Encode(void* encoder, ReadOnlySpan<byte> input,
            int frameSize, Span<byte> output)
        {
            fixed (byte* pcm = input)
            fixed (byte* data = output)
                return Encode(encoder, pcm, frameSize, data, output.Length);
        }

        public enum Application
        {
            Voice = 2048,
            MusicOrMixed = 2049,
            LowLatency = 2051
        }
    }

    public unsafe class OpusEncoder : IDisposable
    {
        private void* _encoder;
        private bool _disposed = false;

        internal OpusEncoder(void* encoder)
        {
            _encoder = encoder;

            SetCtl(Ctl.SetSignal, 3002); // music
            SetCtl(Ctl.SetBitrate, 320000); // 320kbps
            SetCtl(Ctl.SetComplexity, 10); // 10
            SetCtl(Ctl.SetDTX, 1); // true
            SetCtl(Ctl.SetInbandFEC, 1); // true
            SetCtl(Ctl.SetPacketLossPercent, 5); // 5%
        }

        ~OpusEncoder()
        {
           Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                if (_encoder != null)
                    Opus.DestroyEncoder(_encoder);
                _encoder = null;

                _disposed = true;
            }
        }

        public void SetCtl(Ctl request, int value)
        {
            int error = Opus.EncoderCtl(_encoder, (int)request, value);

            if (error != 0)
                throw new Exception("failed to set ctl");
        }

        public int Encode(ReadOnlySpan<byte> input, int frameSize,
            Span<byte> output)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(OpusEncoder));

            return Opus.Encode(_encoder, input, frameSize, output);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public enum Ctl
        {
            SetBitrate = 4002,
            SetBandwidth = 4008,
            SetComplexity = 4010,
            SetInbandFEC = 4012,
            SetPacketLossPercent = 4014,
            SetDTX = 4016,
            SetSignal = 4024
        }
    }
}