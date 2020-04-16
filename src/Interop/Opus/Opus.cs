using System.Runtime.InteropServices;
using Thermite.Utilities;

namespace Thermite.Interop
{
    public static unsafe partial class Opus
    {
        private const string libraryPath = "opus";

        [DllImport(libraryPath, EntryPoint = "opus_encoder_get_size", CallingConvention = CallingConvention.Cdecl)]
        public static extern int opus_encoder_get_size(int channels);

        [DllImport(libraryPath, EntryPoint = "opus_encoder_create", CallingConvention = CallingConvention.Cdecl)]
        [return: NativeTypeName("OpusEncoder *")]
        public static extern OpusEncoder* opus_encoder_create([NativeTypeName("opus_int32")] int Fs, int channels, int application, [NativeTypeName("int *")] int* error);

        [DllImport(libraryPath, EntryPoint = "opus_encoder_init", CallingConvention = CallingConvention.Cdecl)]
        public static extern int opus_encoder_init([NativeTypeName("OpusEncoder *")] OpusEncoder* st, [NativeTypeName("opus_int32")] int Fs, int channels, int application);

        [DllImport(libraryPath, EntryPoint = "opus_encode", CallingConvention = CallingConvention.Cdecl)]
        [return: NativeTypeName("opus_int32")]
        public static extern int opus_encode([NativeTypeName("OpusEncoder *")] OpusEncoder* st, [NativeTypeName("const opus_int16 *")] short* pcm, int frame_size, [NativeTypeName("unsigned char *")] byte* data, [NativeTypeName("opus_int32")] int max_data_bytes);

        [DllImport(libraryPath, EntryPoint = "opus_encode_float", CallingConvention = CallingConvention.Cdecl)]
        [return: NativeTypeName("opus_int32")]
        public static extern int opus_encode_float([NativeTypeName("OpusEncoder *")] OpusEncoder* st, [NativeTypeName("const float *")] float* pcm, int frame_size, [NativeTypeName("unsigned char *")] byte* data, [NativeTypeName("opus_int32")] int max_data_bytes);

        [DllImport(libraryPath, EntryPoint = "opus_encoder_destroy", CallingConvention = CallingConvention.Cdecl)]
        public static extern void opus_encoder_destroy([NativeTypeName("OpusEncoder *")] OpusEncoder* st);

        [DllImport(libraryPath, EntryPoint = "opus_encoder_ctl", CallingConvention = CallingConvention.Cdecl)]
        public static extern int opus_encoder_ctl([NativeTypeName("OpusEncoder *")] OpusEncoder* st, int request);

        [DllImport(libraryPath, EntryPoint = "opus_decoder_get_size", CallingConvention = CallingConvention.Cdecl)]
        public static extern int opus_decoder_get_size(int channels);

        [DllImport(libraryPath, EntryPoint = "opus_decoder_create", CallingConvention = CallingConvention.Cdecl)]
        [return: NativeTypeName("OpusDecoder *")]
        public static extern OpusDecoder* opus_decoder_create([NativeTypeName("opus_int32")] int Fs, int channels, [NativeTypeName("int *")] int* error);

        [DllImport(libraryPath, EntryPoint = "opus_decoder_init", CallingConvention = CallingConvention.Cdecl)]
        public static extern int opus_decoder_init([NativeTypeName("OpusDecoder *")] OpusDecoder* st, [NativeTypeName("opus_int32")] int Fs, int channels);

        [DllImport(libraryPath, EntryPoint = "opus_decode", CallingConvention = CallingConvention.Cdecl)]
        public static extern int opus_decode([NativeTypeName("OpusDecoder *")] OpusDecoder* st, [NativeTypeName("const unsigned char *")] byte* data, [NativeTypeName("opus_int32")] int len, [NativeTypeName("opus_int16 *")] short* pcm, int frame_size, int decode_fec);

        [DllImport(libraryPath, EntryPoint = "opus_decode_float", CallingConvention = CallingConvention.Cdecl)]
        public static extern int opus_decode_float([NativeTypeName("OpusDecoder *")] OpusDecoder* st, [NativeTypeName("const unsigned char *")] byte* data, [NativeTypeName("opus_int32")] int len, [NativeTypeName("float *")] float* pcm, int frame_size, int decode_fec);

        [DllImport(libraryPath, EntryPoint = "opus_decoder_ctl", CallingConvention = CallingConvention.Cdecl)]
        public static extern int opus_decoder_ctl([NativeTypeName("OpusDecoder *")] OpusDecoder* st, int request);

        [DllImport(libraryPath, EntryPoint = "opus_decoder_destroy", CallingConvention = CallingConvention.Cdecl)]
        public static extern void opus_decoder_destroy([NativeTypeName("OpusDecoder *")] OpusDecoder* st);

        [DllImport(libraryPath, EntryPoint = "opus_packet_parse", CallingConvention = CallingConvention.Cdecl)]
        public static extern int opus_packet_parse([NativeTypeName("const unsigned char *")] byte* data, [NativeTypeName("opus_int32")] int len, [NativeTypeName("unsigned char *")] byte* out_toc, [NativeTypeName("const unsigned char *[48]")] byte** frames, [NativeTypeName("opus_int16 [48]")] short* size, [NativeTypeName("int *")] int* payload_offset);

        [DllImport(libraryPath, EntryPoint = "opus_packet_get_bandwidth", CallingConvention = CallingConvention.Cdecl)]
        public static extern int opus_packet_get_bandwidth([NativeTypeName("const unsigned char *")] byte* data);

        [DllImport(libraryPath, EntryPoint = "opus_packet_get_samples_per_frame", CallingConvention = CallingConvention.Cdecl)]
        public static extern int opus_packet_get_samples_per_frame([NativeTypeName("const unsigned char *")] byte* data, [NativeTypeName("opus_int32")] int Fs);

        [DllImport(libraryPath, EntryPoint = "opus_packet_get_nb_channels", CallingConvention = CallingConvention.Cdecl)]
        public static extern int opus_packet_get_nb_channels([NativeTypeName("const unsigned char *")] byte* data);

        [DllImport(libraryPath, EntryPoint = "opus_packet_get_nb_frames", CallingConvention = CallingConvention.Cdecl)]
        public static extern int opus_packet_get_nb_frames([NativeTypeName("const unsigned char []")] byte packet, [NativeTypeName("opus_int32")] int len);

        [DllImport(libraryPath, EntryPoint = "opus_packet_get_nb_samples", CallingConvention = CallingConvention.Cdecl)]
        public static extern int opus_packet_get_nb_samples([NativeTypeName("const unsigned char []")] byte packet, [NativeTypeName("opus_int32")] int len, [NativeTypeName("opus_int32")] int Fs);

        [DllImport(libraryPath, EntryPoint = "opus_decoder_get_nb_samples", CallingConvention = CallingConvention.Cdecl)]
        public static extern int opus_decoder_get_nb_samples([NativeTypeName("const OpusDecoder *")] OpusDecoder* dec, [NativeTypeName("const unsigned char []")] byte packet, [NativeTypeName("opus_int32")] int len);

        [DllImport(libraryPath, EntryPoint = "opus_pcm_soft_clip", CallingConvention = CallingConvention.Cdecl)]
        public static extern void opus_pcm_soft_clip([NativeTypeName("float *")] float* pcm, int frame_size, int channels, [NativeTypeName("float *")] float* softclip_mem);

        [DllImport(libraryPath, EntryPoint = "opus_repacketizer_get_size", CallingConvention = CallingConvention.Cdecl)]
        public static extern int opus_repacketizer_get_size();

        [DllImport(libraryPath, EntryPoint = "opus_repacketizer_init", CallingConvention = CallingConvention.Cdecl)]
        [return: NativeTypeName("OpusRepacketizer *")]
        public static extern OpusRepacketizer* opus_repacketizer_init([NativeTypeName("OpusRepacketizer *")] OpusRepacketizer* rp);

        [DllImport(libraryPath, EntryPoint = "opus_repacketizer_create", CallingConvention = CallingConvention.Cdecl)]
        [return: NativeTypeName("OpusRepacketizer *")]
        public static extern OpusRepacketizer* opus_repacketizer_create();

        [DllImport(libraryPath, EntryPoint = "opus_repacketizer_destroy", CallingConvention = CallingConvention.Cdecl)]
        public static extern void opus_repacketizer_destroy([NativeTypeName("OpusRepacketizer *")] OpusRepacketizer* rp);

        [DllImport(libraryPath, EntryPoint = "opus_repacketizer_cat", CallingConvention = CallingConvention.Cdecl)]
        public static extern int opus_repacketizer_cat([NativeTypeName("OpusRepacketizer *")] OpusRepacketizer* rp, [NativeTypeName("const unsigned char *")] byte* data, [NativeTypeName("opus_int32")] int len);

        [DllImport(libraryPath, EntryPoint = "opus_repacketizer_out_range", CallingConvention = CallingConvention.Cdecl)]
        [return: NativeTypeName("opus_int32")]
        public static extern int opus_repacketizer_out_range([NativeTypeName("OpusRepacketizer *")] OpusRepacketizer* rp, int begin, int end, [NativeTypeName("unsigned char *")] byte* data, [NativeTypeName("opus_int32")] int maxlen);

        [DllImport(libraryPath, EntryPoint = "opus_repacketizer_get_nb_frames", CallingConvention = CallingConvention.Cdecl)]
        public static extern int opus_repacketizer_get_nb_frames([NativeTypeName("OpusRepacketizer *")] OpusRepacketizer* rp);

        [DllImport(libraryPath, EntryPoint = "opus_repacketizer_out", CallingConvention = CallingConvention.Cdecl)]
        [return: NativeTypeName("opus_int32")]
        public static extern int opus_repacketizer_out([NativeTypeName("OpusRepacketizer *")] OpusRepacketizer* rp, [NativeTypeName("unsigned char *")] byte* data, [NativeTypeName("opus_int32")] int maxlen);

        [DllImport(libraryPath, EntryPoint = "opus_packet_pad", CallingConvention = CallingConvention.Cdecl)]
        public static extern int opus_packet_pad([NativeTypeName("unsigned char *")] byte* data, [NativeTypeName("opus_int32")] int len, [NativeTypeName("opus_int32")] int new_len);

        [DllImport(libraryPath, EntryPoint = "opus_packet_unpad", CallingConvention = CallingConvention.Cdecl)]
        [return: NativeTypeName("opus_int32")]
        public static extern int opus_packet_unpad([NativeTypeName("unsigned char *")] byte* data, [NativeTypeName("opus_int32")] int len);

        [DllImport(libraryPath, EntryPoint = "opus_multistream_packet_pad", CallingConvention = CallingConvention.Cdecl)]
        public static extern int opus_multistream_packet_pad([NativeTypeName("unsigned char *")] byte* data, [NativeTypeName("opus_int32")] int len, [NativeTypeName("opus_int32")] int new_len, int nb_streams);

        [DllImport(libraryPath, EntryPoint = "opus_multistream_packet_unpad", CallingConvention = CallingConvention.Cdecl)]
        [return: NativeTypeName("opus_int32")]
        public static extern int opus_multistream_packet_unpad([NativeTypeName("unsigned char *")] byte* data, [NativeTypeName("opus_int32")] int len, int nb_streams);
    }
}
