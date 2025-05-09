using System.Net;
using System.Text.Json.Serialization;

namespace FolderPorter.Model
{
    [Serializable]
    public class RemoteDeviceModel
    {
        public string IP { get; set; }
        public string DevicePassword { get; set; }

        [JsonIgnore]
        public IPEndPoint IPEndPoint { get; set; }
        [JsonIgnore]
        public string DeviceName { get; set; }
    }
}