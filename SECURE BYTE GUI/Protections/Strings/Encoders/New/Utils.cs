﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Protections.newStrings
{
    public static class Utils
    {
        public static byte[] Read(Stream stream)
        {
            byte[] array;
            using (MemoryStream memoryStream = new System.IO.MemoryStream())
            {
                stream.CopyTo(memoryStream);
                array = memoryStream.ToArray();
            }
            return array;
        }
        #region Decompression
        public const int QLZ_VERSION_MAJOR = 1;
        public const int QLZ_VERSION_MINOR = 5;
        public const int QLZ_VERSION_REVISION = 0;
        public const int QLZ_STREAMING_BUFFER = 0;
        public const int QLZ_MEMORY_SAFE = 0;
        private const int HASH_VALUES = 4096;
        private const int UNCONDITIONAL_MATCHLEN = 6;
        private const int UNCOMPRESSED_END = 4;
        private const int CWORD_LEN = 4;
        public static byte[] DecompressBytes(byte[] source, int rnd)
        {
            int level;
            byte[] add = new byte[] { (byte)rnd };
            byte[] concat = source.Concat(add).ToArray();
            source = concat;
            int size = SizeDecompressed(source);
            int src = HeaderLen(source);
            int dst = 0;
            uint cword_val = 1;
            byte[] destination = new byte[size];
            int[] hashtable = new int[4096];
            byte[] hash_counter = new byte[4096];
            int last_matchstart = size - UNCONDITIONAL_MATCHLEN - UNCOMPRESSED_END - 1;
            int last_hashed = -1;
            int hash;
            uint fetch = 0;

            level = (source[0] >> 2) & 0x3;

            if (level != 1 && level != 3) ;
            //throw new ArgumentException("C# version only supports level 1 and 3");

            if ((source[0] & 1) != 1)
            {
                byte[] d2 = new byte[size];
                System.Array.Copy(source, HeaderLen(source), d2, 0, size);
                return d2;
            }

            for (; ; )
            {
                if (cword_val == 1)
                {
                    cword_val = (uint)(source[src] | (source[src + 1] << 8) | (source[src + 2] << 16) | (source[src + 3] << 24));
                    src += 4;
                    if (dst <= last_matchstart)
                    {
                        if (level == 1)
                            fetch = (uint)(source[src] | (source[src + 1] << 8) | (source[src + 2] << 16));
                        else
                            fetch = (uint)(source[src] | (source[src + 1] << 8) | (source[src + 2] << 16) | (source[src + 3] << 24));
                    }
                }

                if ((cword_val & 1) == 1)
                {
                    uint matchlen;
                    uint offset2;

                    cword_val = cword_val >> 1;

                    if (level == 1)
                    {
                        hash = ((int)fetch >> 4) & 0xfff;
                        offset2 = (uint)hashtable[hash];

                        if ((fetch & 0xf) != 0)
                        {
                            matchlen = (fetch & 0xf) + 2;
                            src += 2;
                        }
                        else
                        {
                            matchlen = source[src + 2];
                            src += 3;
                        }
                    }
                    else
                    {
                        uint offset;
                        if ((fetch & 3) == 0)
                        {
                            offset = (fetch & 0xff) >> 2;
                            matchlen = 3;
                            src++;
                        }
                        else if ((fetch & 2) == 0)
                        {
                            offset = (fetch & 0xffff) >> 2;
                            matchlen = 3;
                            src += 2;
                        }
                        else if ((fetch & 1) == 0)
                        {
                            offset = (fetch & 0xffff) >> 6;
                            matchlen = ((fetch >> 2) & 15) + 3;
                            src += 2;
                        }
                        else if ((fetch & 127) != 3)
                        {
                            offset = (fetch >> 7) & 0x1ffff;
                            matchlen = ((fetch >> 2) & 0x1f) + 2;
                            src += 3;
                        }
                        else
                        {
                            offset = (fetch >> 15);
                            matchlen = ((fetch >> 7) & 255) + 3;
                            src += 4;
                        }
                        offset2 = (uint)(dst - offset);
                    }

                    destination[dst + 0] = destination[offset2 + 0];
                    destination[dst + 1] = destination[offset2 + 1];
                    destination[dst + 2] = destination[offset2 + 2];

                    for (int i = 3; i < matchlen; i += 1)
                    {
                        destination[dst + i] = destination[offset2 + i];
                    }

                    dst += (int)matchlen;

                    if (level == 1)
                    {
                        fetch = (uint)(destination[last_hashed + 1] | (destination[last_hashed + 2] << 8) | (destination[last_hashed + 3] << 16));
                        while (last_hashed < dst - matchlen)
                        {
                            last_hashed++;
                            hash = (int)(((fetch >> 12) ^ fetch) & (HASH_VALUES - 1));
                            hashtable[hash] = last_hashed;
                            hash_counter[hash] = 1;
#pragma warning disable CS0675 // Bitwise-or operator used on a sign-extended operand
                            fetch = (uint)(fetch >> 8 & 0xffff | destination[last_hashed + 3] << 16);
#pragma warning restore CS0675 // Bitwise-or operator used on a sign-extended operand
                        }
                        fetch = (uint)(source[src] | (source[src + 1] << 8) | (source[src + 2] << 16));
                    }
                    else
                    {
                        fetch = (uint)(source[src] | (source[src + 1] << 8) | (source[src + 2] << 16) | (source[src + 3] << 24));
                    }
                    last_hashed = dst - 1;
                }
                else
                {
                    if (dst <= last_matchstart)
                    {
                        destination[dst] = source[src];
                        dst += 1;
                        src += 1;
                        cword_val = cword_val >> 1;

                        if (level == 1)
                        {
                            while (last_hashed < dst - 3)
                            {
                                last_hashed++;
                                int fetch2 = destination[last_hashed] | (destination[last_hashed + 1] << 8) | (destination[last_hashed + 2] << 16);
                                hash = ((fetch2 >> 12) ^ fetch2) & (HASH_VALUES - 1);
                                hashtable[hash] = last_hashed;
                                hash_counter[hash] = 1;
                            }
#pragma warning disable CS0675 // Bitwise-or operator used on a sign-extended operand
                            fetch = (uint)(fetch >> 8 & 0xffff | source[src + 2] << 16);
#pragma warning restore CS0675 // Bitwise-or operator used on a sign-extended operand
                        }
                        else
                        {
#pragma warning disable CS0675 // Bitwise-or operator used on a sign-extended operand
                            fetch = (uint)(fetch >> 8 & 0xffff | source[src + 2] << 16 | source[src + 3] << 24);
#pragma warning restore CS0675 // Bitwise-or operator used on a sign-extended operand
                        }
                    }
                    else
                    {
                        while (dst <= size - 1)
                        {
                            if (cword_val == 1)
                            {
                                src += CWORD_LEN;
                                cword_val = 0x80000000;
                            }

                            destination[dst] = source[src];
                            dst++;
                            src++;
                            cword_val = cword_val >> 1;
                        }
                        return destination;
                    }
                }
            }
        }
        public static int HeaderLen(byte[] source)
        {
            return ((source[0] & 2) == 2) ? 9 : 3;
        }
        public static int SizeDecompressed(byte[] source)
        {
            if (HeaderLen(source) == 9)
                return source[5] | (source[6] << 8) | (source[7] << 16) | (source[8] << 24);

            return source[2];
        }
        #endregion
        public static void Extract()
        {
            stringsList = new string[0];
            Stream resFilestream = typeof(Utils).Assembly.GetManifestResourceStream("CallMeInx");
            StreamReader streamReader = new StreamReader(new MemoryStream(DecompressBytes(Read(resFilestream), 1)));
            string line;
            while ((line = streamReader.ReadLine()) != null)
            {
                Array.Resize(ref stringsList, stringsList.Length + 1);
                stringsList[stringsList.Length - 1] = line;
            }
        }
        public static string Call(string[] arr, int index)
        {
            string Text = string.Empty;
            if (hTable == null)
            {
                hTable = new Dictionary<string, string>();
            }
            Module module = typeof(Utils).Module;
            Assembly assembly = module.Assembly;
            if (assembly != null)
            {
                Dictionary<string, string> hashtable = hTable;
                lock (hashtable)
                {
                    if (hTable.ContainsKey(arr[index]))
                        Text = hTable[arr[index]];
                    else
                    {

                        Aes aesAlg = Aes.Create();
                        aesAlg.Key = Encoding.UTF8.GetBytes("Key");
                        aesAlg.IV = Encoding.UTF8.GetBytes("IV");
                        ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
                        byte[] cipherBytes = Convert.FromBase64String(arr[index]);
                        using (var msDecrypt = new MemoryStream(cipherBytes))
                        using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        using (var srDecrypt = new StreamReader(csDecrypt))
                        {
                            Text = srDecrypt.ReadToEnd();
                        }
                        hTable[arr[index]] = Text;
                    }
                }
            }
            return Text;
        }
        public static Dictionary<string, string> hTable;
        public static string[] stringsList;
    }
}