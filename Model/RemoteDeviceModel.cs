using System.Net;
using System.Net.Sockets;
using System.Text.Json.Serialization;

namespace FolderPorter.Model
{
    [Serializable]
    public class RemoteDeviceModel
    {
        public string IP { get; set; }
        public string IP2 { get; set; }
        public string DomainPort { get; set; }
        public string DevicePassword { get; set; }

        [JsonIgnore]
        public string DeviceName { get; set; }

        public TcpClient? TryConnect()
        {
            TcpClient tcpClient = null;

            if (string.IsNullOrWhiteSpace(IP))
                goto SkipIP1;
            if (IPEndPoint.TryParse(IP, out IPEndPoint? ip1) &&
                TryConnectIPEndPoint(ip1, out tcpClient))
                goto SkipOtherIP;
        SkipIP1:

            if (string.IsNullOrWhiteSpace(IP2))
                goto SkipIP2;
            if (IPEndPoint.TryParse(IP2, out IPEndPoint? ip2) &&
                TryConnectIPEndPoint(ip2, out tcpClient))
                goto SkipOtherIP;
        SkipIP2:

            if (string.IsNullOrWhiteSpace(DomainPort))
                goto SkipDomainPort;
            int commaIndex = DomainPort.IndexOf(':');
            if (commaIndex == 0)
                goto SkipDomainPort;
            string domainStr = DomainPort.Substring(0, commaIndex).Trim();
            string portStr = DomainPort.Substring(commaIndex + 1).Trim();
            IPHostEntry domainIPHostEntry = Dns.GetHostEntry(domainStr);
            if (domainIPHostEntry.AddressList.Length == 0)
                goto SkipDomainPort;
            int.TryParse(portStr, out int ip3Port);
            IPEndPoint ip3 = new IPEndPoint(domainIPHostEntry.AddressList[0], ip3Port);
            if (TryConnectIPEndPoint(ip3, out tcpClient))
                goto SkipOtherIP;
            SkipDomainPort:

        SkipOtherIP:
            if (AppSettingModel.Instance.LogDebug &&
                tcpClient != null)
                Console.WriteLine($"Try connect {tcpClient.Client.RemoteEndPoint}, Connected: {tcpClient.Connected}.");
            return tcpClient;
        }

        private bool TryConnectIPEndPoint(IPEndPoint iPEndPoint, out TcpClient? tcpClient)
        {
            tcpClient = null;
            bool result;
            try
            {
                tcpClient = new TcpClient();
                AppSettingModel.Instance.SetTcpClientParameter(tcpClient);
                tcpClient.Connect(iPEndPoint);
            }
            catch (Exception)
            {
                if (AppSettingModel.Instance.LogDebug)
                    Console.WriteLine($"Try connect {iPEndPoint} failed.");
            }
            finally
            {
                if (tcpClient == null)
                    result = false;
                else if (!tcpClient.Connected)
                {
                    tcpClient.Dispose();
                    result = false;
                }
                else
                    result = true;
            }
            return result;
        }
    }
}