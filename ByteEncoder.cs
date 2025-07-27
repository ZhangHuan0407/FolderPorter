using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
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
        public static int WriteString(byte[] buffer, string str, int blockRemain, ref int pointer)
        {
            int length = Encoding.UTF8.GetBytes(str, 0, str.Length, buffer, pointer + 8);
            WriteInt(buffer, length, ref pointer);
            WriteInt(buffer, blockRemain, ref pointer);
            pointer += length;
            length += 4;
            return length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ReadString(byte[] buffer, out int blockRemain, ref int pointer)
        {
            int length = ReadInt(buffer, ref pointer);
            blockRemain = ReadInt(buffer, ref pointer);
            if (length <= 0 ||
                buffer.Length - pointer < length)
                throw new ArgumentException($"ReadString with length: {length}");
            if (blockRemain < 0)
                throw new ArgumentException($"ReadString with blockRemain: {blockRemain}");

            string str = Encoding.UTF8.GetString(buffer, pointer, length);
            pointer += length;
            return str;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteStringWithAes(byte[] buffer, ConnectWrapper connectWrapper, string str, int blockRemain, ref int pointer)
        {
            int rawBytesLength = Encoding.UTF8.GetBytes(str, 0, str.Length, connectWrapper.AesBuffer, 0);

            int blockSize = connectWrapper.Aes.BlockSize;
            int fullBlocks = rawBytesLength / blockSize;
            int remaining = rawBytesLength % blockSize;

            int point0 = pointer;
            pointer += 4;

            for (int i = 0; i < fullBlocks; i++)
            {
                int bytesWritten = connectWrapper.Encryptor.TransformBlock(connectWrapper.AesBuffer, i * blockSize, blockSize,
                                                                           buffer, pointer);
                pointer += bytesWritten;
            }

            byte[] finalBlock = connectWrapper.Encryptor.TransformFinalBlock(connectWrapper.AesBuffer, fullBlocks * blockSize, remaining);
            Array.Copy(finalBlock, 0, buffer, pointer, finalBlock.Length);
            pointer += finalBlock.Length;

            WriteInt(buffer, pointer - point0 - 4, ref point0);
            WriteInt(buffer, blockRemain, ref point0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ReadStringWithAes(byte[] buffer, ConnectWrapper connectWrapper, out int blockRemain, ref int pointer)
        {
            int encryptedLength = ReadInt(buffer, ref pointer);
            blockRemain = ReadInt(buffer, ref pointer);
            if (encryptedLength <= 0 || buffer.Length - pointer < encryptedLength)
                throw new ArgumentException($"Invalid encrypted length: {encryptedLength}");

            int blockSize = connectWrapper.Aes.BlockSize;
            if (encryptedLength % blockSize != 0)
                throw new ArgumentException("Encrypted data length is not block aligned");

            int outputPointer = 0;
            int fullBlocks = encryptedLength / blockSize;

            for (int i = 0; i < fullBlocks; i++)
            {
                int bytesRead = connectWrapper.Decryptor.TransformBlock(buffer, pointer + i * blockSize, blockSize,
                                                                        connectWrapper.AesBuffer, outputPointer);
                outputPointer += bytesRead;
            }

            byte[] finalBlock = connectWrapper.Decryptor.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            Array.Copy(finalBlock, 0, connectWrapper.AesBuffer, outputPointer, finalBlock.Length);
            outputPointer += finalBlock.Length;

            pointer += encryptedLength;
            return Encoding.UTF8.GetString(connectWrapper.AesBuffer, 0, outputPointer);
        }

        public static void EncryptedWithAes(byte[] buffer, ConnectWrapper connectWrapper, int pointer, ref int length)
        {
            int blockSize = connectWrapper.Aes.BlockSize;
            int fullBlocks = length / blockSize;
            int remaining = length % blockSize;

            int point0 = pointer;

            for (int i = 0; i < fullBlocks; i++)
            {
                int bytesWritten = connectWrapper.Encryptor.TransformBlock(connectWrapper.AesBuffer, i * blockSize, blockSize,
                                                                           buffer, pointer);
                pointer += bytesWritten;
            }
            byte[] finalBlock = connectWrapper.Encryptor.TransformFinalBlock(connectWrapper.AesBuffer, fullBlocks * blockSize, remaining);
            Array.Copy(finalBlock, 0, buffer, pointer, finalBlock.Length);
            pointer += finalBlock.Length;
            length = pointer - point0;
        }

        public static void DecryptedWithAes(byte[] buffer, ConnectWrapper connectWrapper, int pointer, ref int length)
        {
            int blockSize = connectWrapper.Aes.BlockSize;
            if (length % blockSize != 0)
                throw new ArgumentException("Encrypted data length is not block aligned");

            int outputPointer = 0;
            int fullBlocks = length / blockSize;
            for (int i = 0; i < fullBlocks; i++)
            {
                int bytesRead = connectWrapper.Decryptor.TransformBlock(connectWrapper.AesBuffer, pointer + i * blockSize, blockSize,
                                                                        buffer, outputPointer);
                outputPointer += bytesRead;
            }
            byte[] finalBlock = connectWrapper.Decryptor.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            Array.Copy(finalBlock, 0, buffer, outputPointer, finalBlock.Length);
            outputPointer += finalBlock.Length;
            length = outputPointer;
        }

        public static void CreateTimebasedPassword(string password, out string result0, out string result1)
        {
            result0 = result1 = string.Empty;

            long ts = (long)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
            long ts1 = (ts - 14L) / 30L * 30L;
            long ts2 = (ts + 14L) / 30L * 30L;

            byte[] buffer = new byte[1024];
            int bytesCount = Encoding.UTF8.GetBytes($"{password}_{ts1}", buffer);
            using (MD5 md5 = MD5.Create())
            {
                byte[] hashResult = md5.ComputeHash(buffer, 0, bytesCount);
                result0 = Convert.ToHexString(hashResult);
            }

            bytesCount = Encoding.UTF8.GetBytes($"{password}_{ts2}", buffer);
            using (MD5 md5 = MD5.Create())
            {
                byte[] hashResult = md5.ComputeHash(buffer, 0, bytesCount);
                result1 = Convert.ToHexString(hashResult);
            }
        }
    }
}