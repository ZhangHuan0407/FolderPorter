using System.Net.Sockets;
using System.Security.Cryptography;

namespace FolderPorter
{
    public class ConnectWrapper
    {
        public readonly TcpClient TcpClient;
        public readonly NetworkStream NetworkStream;
        public readonly Guid TaskGuid;
        public EncryptedTransmission EncryptedTransmission;
        
        public Aes Aes { get; private set; }
        public ICryptoTransform Encryptor { get; private set; }
        public ICryptoTransform Decryptor { get; private set; }
        public byte[] AesBuffer;

        public ConnectWrapper(TcpClient tcpClient, Guid taskGuid)
        {
            TcpClient = tcpClient;
            NetworkStream = tcpClient.GetStream();
            TaskGuid = taskGuid;
        }

        public void CreateAes(byte[] buffer)
        {
            byte[] key = new byte[16];
            Array.Copy(buffer, 4, key, 0, 16);
            Aes = Aes.Create();
            Aes.Key = key;
            Aes.Mode = CipherMode.CBC;
            Aes.Padding = PaddingMode.PKCS7;
            byte[] iv = new byte[16];
            Array.Copy(buffer, 20, iv, 0, 16);
            Aes.IV = iv;

            Encryptor = Aes.CreateEncryptor();
            Decryptor = Aes.CreateDecryptor();
        }
    }
}