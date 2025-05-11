using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FolderPorter.Model
{
    [Serializable]
    public class AppSettingModel
    {
        private static string AppSettingsTemplateFileName = $"{Path.GetDirectoryName(Environment.ProcessPath)}/AppSettingsTemplate.json";
        private static string AppSettingsFileName = $"{Path.GetDirectoryName(Environment.ProcessPath)}/AppSettings.json";

        internal static AppSettingModel Instance = new AppSettingModel();

        public string Password { get; set; }

        public Dictionary<string, FolderModel> LocalFolders { get; set; }

        public Dictionary<string, RemoteDeviceModel> RemoteDevice { get; set; }

        public int MaxWorkerThreadCount { get; set; }
        public int MaxIOThreadCount { get; set; }

        public int RemoteBuzyRetrySeconds { get; set; }
        [JsonIgnore]
        public TimeSpan RemoteBuzyRetryTimeSpan { get; set; }

        public int SocketBufferSize { get; set; }
        public int ConnectTimeoutMS { get; set; }

        public int ListernPort { get; set; }

        public bool LogDebug { get; set; }
        public bool LogProtocal { get; set; }

        public static bool IsTemplate;

        public static void Reload()
        {
            string appSettingJson;
            if (File.Exists(AppSettingsFileName))
            {
                appSettingJson = File.ReadAllText(AppSettingsFileName);
                IsTemplate = false;
            }
            else
            {
                appSettingJson = File.ReadAllText(AppSettingsTemplateFileName);
                IsTemplate = true;
            }
            AppSettingModel appSettingModel = JsonSerializer.Deserialize<AppSettingModel>(appSettingJson)!;

            foreach (KeyValuePair<string, FolderModel> pair in appSettingModel.LocalFolders)
                pair.Value.Folder = pair.Key;
            foreach (KeyValuePair<string, RemoteDeviceModel> pair in appSettingModel.RemoteDevice)
                pair.Value.DeviceName = pair.Key;

            appSettingModel.RemoteBuzyRetryTimeSpan = TimeSpan.FromSeconds(appSettingModel.RemoteBuzyRetrySeconds);
            Instance = appSettingModel;
        }

        public static void CopyAppSettings()
        {
            Console.WriteLine("You dont have AppSettings.json file, copy a new file? y/n");
            while (true)
            {
                char keyChar = Console.ReadKey().KeyChar;
                if (keyChar == 'y')
                {
                    File.Copy(AppSettingsTemplateFileName, AppSettingsFileName);
                    Console.WriteLine();
                    return;
                }
                else if (keyChar == 'n')
                {
                    Console.WriteLine();
                    return;
                }
            }
        }

        public void SetTcpClientParameter(TcpClient tcpClient)
        {
            tcpClient.SendBufferSize = SocketBufferSize;
            tcpClient.ReceiveBufferSize = SocketBufferSize;
            tcpClient.SendTimeout = ConnectTimeoutMS;
            tcpClient.ReceiveTimeout = ConnectTimeoutMS;
        }
    }
}