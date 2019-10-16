using System;
using System.Runtime.InteropServices;
using Thermite.Utilities;

namespace Thermite.Natives
{
    internal static unsafe partial class Sodium
    {
        private const string libraryPath = "sodium";

        [DllImport(libraryPath, EntryPoint = "randombytes_buf", CallingConvention = CallingConvention.Cdecl)]
        public static extern void randombytes_buf([NativeTypeName("void * const")] void* buf, [NativeTypeName("const size_t")] UIntPtr size);

        [DllImport(libraryPath, EntryPoint = "crypto_secretbox_keybytes", CallingConvention = CallingConvention.Cdecl)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr crypto_secretbox_keybytes();

        [DllImport(libraryPath, EntryPoint = "crypto_secretbox_noncebytes", CallingConvention = CallingConvention.Cdecl)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr crypto_secretbox_noncebytes();

        [DllImport(libraryPath, EntryPoint = "crypto_secretbox_macbytes", CallingConvention = CallingConvention.Cdecl)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr crypto_secretbox_macbytes();

        [DllImport(libraryPath, EntryPoint = "crypto_secretbox_primitive", CallingConvention = CallingConvention.Cdecl)]
        [return: NativeTypeName("const char *")]
        public static extern sbyte* crypto_secretbox_primitive();

        [DllImport(libraryPath, EntryPoint = "crypto_secretbox_messagebytes_max", CallingConvention = CallingConvention.Cdecl)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr crypto_secretbox_messagebytes_max();

        [DllImport(libraryPath, EntryPoint = "crypto_secretbox_easy", CallingConvention = CallingConvention.Cdecl)]
        public static extern int crypto_secretbox_easy([NativeTypeName("unsigned char *")] byte* c, [NativeTypeName("const unsigned char *")] byte* m, [NativeTypeName("unsigned long long")] ulong mlen, [NativeTypeName("const unsigned char *")] byte* n, [NativeTypeName("const unsigned char *")] byte* k);

        [DllImport(libraryPath, EntryPoint = "crypto_secretbox_open_easy", CallingConvention = CallingConvention.Cdecl)]
        public static extern int crypto_secretbox_open_easy([NativeTypeName("unsigned char *")] byte* m, [NativeTypeName("const unsigned char *")] byte* c, [NativeTypeName("unsigned long long")] ulong clen, [NativeTypeName("const unsigned char *")] byte* n, [NativeTypeName("const unsigned char *")] byte* k);

        [DllImport(libraryPath, EntryPoint = "crypto_secretbox_detached", CallingConvention = CallingConvention.Cdecl)]
        public static extern int crypto_secretbox_detached([NativeTypeName("unsigned char *")] byte* c, [NativeTypeName("unsigned char *")] byte* mac, [NativeTypeName("const unsigned char *")] byte* m, [NativeTypeName("unsigned long long")] ulong mlen, [NativeTypeName("const unsigned char *")] byte* n, [NativeTypeName("const unsigned char *")] byte* k);

        [DllImport(libraryPath, EntryPoint = "crypto_secretbox_open_detached", CallingConvention = CallingConvention.Cdecl)]
        public static extern int crypto_secretbox_open_detached([NativeTypeName("unsigned char *")] byte* m, [NativeTypeName("const unsigned char *")] byte* c, [NativeTypeName("const unsigned char *")] byte* mac, [NativeTypeName("unsigned long long")] ulong clen, [NativeTypeName("const unsigned char *")] byte* n, [NativeTypeName("const unsigned char *")] byte* k);

        [DllImport(libraryPath, EntryPoint = "crypto_secretbox_keygen", CallingConvention = CallingConvention.Cdecl)]
        public static extern void crypto_secretbox_keygen([NativeTypeName("unsigned char [32]")] byte k);

        [DllImport(libraryPath, EntryPoint = "crypto_secretbox_zerobytes", CallingConvention = CallingConvention.Cdecl)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr crypto_secretbox_zerobytes();

        [DllImport(libraryPath, EntryPoint = "crypto_secretbox_boxzerobytes", CallingConvention = CallingConvention.Cdecl)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr crypto_secretbox_boxzerobytes();

        [DllImport(libraryPath, EntryPoint = "crypto_secretbox", CallingConvention = CallingConvention.Cdecl)]
        public static extern int crypto_secretbox([NativeTypeName("unsigned char *")] byte* c, [NativeTypeName("const unsigned char *")] byte* m, [NativeTypeName("unsigned long long")] ulong mlen, [NativeTypeName("const unsigned char *")] byte* n, [NativeTypeName("const unsigned char *")] byte* k);

        [DllImport(libraryPath, EntryPoint = "crypto_secretbox_open", CallingConvention = CallingConvention.Cdecl)]
        public static extern int crypto_secretbox_open([NativeTypeName("unsigned char *")] byte* m, [NativeTypeName("const unsigned char *")] byte* c, [NativeTypeName("unsigned long long")] ulong clen, [NativeTypeName("const unsigned char *")] byte* n, [NativeTypeName("const unsigned char *")] byte* k);
    }
}
