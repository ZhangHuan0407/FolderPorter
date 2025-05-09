using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace FolderPorter
{
    public static class ByteEncoder
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt(byte[] buffer, ref int pointer)
        {
            int value;
            value = buffer[pointer] |
                    buffer[pointer + 1] << 8 |
                    buffer[pointer + 2] << 16 |
                    buffer[pointer + 3] << 24;
            pointer += 4;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt(byte[] buffer, int value, ref int pointer)
        {
            buffer[pointer] = (byte)value;
            value >>= 8;
            buffer[pointer + 1] = (byte)value;
            value >>= 8;
            buffer[pointer + 2] = (byte)value;
            value >>= 8;
            buffer[pointer + 3] = (byte)value;
            pointer += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteString(byte[] buffer, string str, ref int pointer)
        {
            int length = Encoding.UTF8.GetBytes(str, 0, str.Length, buffer, pointer + 4);
            WriteInt(buffer, length, ref pointer);
            pointer += length;
            length += 4;
            return length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ReadString(byte[] buffer, ref int pointer)
        {
            int length = ReadInt(buffer, ref pointer);
            if (length <= 0 ||
                buffer.Length - pointer < length)
            {
                throw new ArgumentException($"ReadString with length: {length}");
            }
            string str = Encoding.UTF8.GetString(buffer, pointer, length);
            pointer += length;
            return str;
        }
    }
}