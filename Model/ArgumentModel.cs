using System.Text.RegularExpressions;

namespace FolderPorter.Model
{
    [Serializable]
    public class ArgumentModel
    {
        public static ArgumentModel Instance { get; set; }

        public bool Server { get; set; }

        public string PushRemoteDrive { get; set; }
        public string PushFolder { get; set; }

        public string PullRemoteDrive { get; set; }
        public string PullFolder { get; set; }

        public bool Help { get; set; }
        public bool InteractiveMode { get; set; }

        public static void ParseArgs(string[] args)
        {
            Instance = new ArgumentModel();
            for (int i = 0; i < args.Length; i++)
            {
                string toLower = args[i].ToLower().Trim('-');
                if (toLower == Program.ServerType)
                {
                    Instance.Server = true;
                }
                else if (toLower.StartsWith(Program.PushType))
                {
                    Match match = Regex.Match(args[i], "@(?<RemoteDrive>[\\w_-]+):(?<Folder>[\\w_-]+)\\s?\\Z");
                    if (match.Success)
                    {
                        Instance.PushRemoteDrive = match.Groups["RemoteDrive"]?.Value ?? string.Empty;
                        Instance.PushFolder = match.Groups["Folder"]?.Value ?? string.Empty;
                    }
                    if (string.IsNullOrEmpty(Instance.PushRemoteDrive) ||
                        string.IsNullOrEmpty(Instance.PushFolder))
                        throw new Exception("push argument error");
                }
                else if (toLower.StartsWith(Program.PullType))
                {
                    Match match = Regex.Match(args[i], "@(?<RemoteDrive>[\\w_-]+):(?<Folder>[\\w_-]+)\\s?\\Z");
                    if (match.Success)
                    {
                        Instance.PullRemoteDrive = match.Groups["RemoteDrive"]?.Value ?? string.Empty;
                        Instance.PullFolder = match.Groups["Folder"]?.Value ?? string.Empty;
                    }
                    if (string.IsNullOrEmpty(Instance.PullRemoteDrive) ||
                        string.IsNullOrEmpty(Instance.PullFolder))
                        throw new Exception("pull argument error");
                }
                else if (toLower == "help")
                {
                    Instance.Help = true;
                }
            }
            Instance.Help |= args.Length == 0;
            Instance.InteractiveMode = args.Length == 0;
        }

        public static void LogHelp()
        {
            Console.WriteLine(@"Help: 
    server                    enable server mode and listern port appsetting.json[ListernPort]
                              app will listen for a long time and the port will not automatically exit.

    push@RemoteDrive:Folder   example: push@raspberry4:testfolder
                              try to connect raspberry4 in appsetting.json[RemoteDevice] and
                              push all files in appsetting.json[LocalFolders][Folder] to remote folder.

    pull@RemoteDrive:Folder   example: pull@linux:folder2
                              try to connect linux in appsetting.json[RemoteDevice] and
                              pull all files in appsetting.json[LocalFolders][Folder] from remote folder.
");
        }

        public void EnterInteractiveMode()
        {
            Console.WriteLine("push, pull or server?");
            string type;
            while (true)
            {
                type = Console.ReadLine()!.ToLower().Trim();
                if (type != Program.PushType &&
                    type != Program.PullType &&
                    type != Program.ServerType)
                    continue;
                break;
            }
            if (type == Program.ServerType)
            {
                Server = true;
                return;
            }

            Console.WriteLine("select one of remote drive (input name or number):");
            List<string> remoteDeviceList = new List<string>();
            foreach (string remoteDevice in AppSettingModel.Instance.RemoteDevice.Keys)
            {
                Console.WriteLine($"[{remoteDeviceList.Count.ToString().PadLeft(2)}] {remoteDevice}");
                remoteDeviceList.Add(remoteDevice);
            }
            string inputRemoteDevice;
            while (true)
            {
                string input = Console.ReadLine()!.Trim();
                if (AppSettingModel.Instance.RemoteDevice.ContainsKey(input))
                {
                    inputRemoteDevice = input;
                    break;
                }
                if (int.TryParse(input, out int index) &&
                    index >= 0 && index < remoteDeviceList.Count)
                {
                    inputRemoteDevice = remoteDeviceList[index];
                    break;
                }
            }

            Console.WriteLine("select one of folder (input name or number):");
            List<string> folderList = new List<string>();
            foreach (string folder in AppSettingModel.Instance.LocalFolders.Keys)
            {
                Console.WriteLine($"[{folderList.Count.ToString().PadLeft(2)}] {folder}");
                folderList.Add(folder);
            }
            string inputFolder;
            while (true)
            {
                string input = Console.ReadLine()!.Trim();
                if (AppSettingModel.Instance.LocalFolders.ContainsKey(input))
                {
                    inputFolder = input;
                    break;
                }
                if (int.TryParse(input, out int index) &&
                    index >= 0 && index < folderList.Count)
                {
                    inputFolder = folderList[index];
                    break;
                }
            }

            if (type == Program.PushType)
            {
                PushRemoteDrive = inputRemoteDevice;
                PushFolder = inputFolder;
            }
            else if (type == Program.PullType)
            {
                PullRemoteDrive = inputRemoteDevice;
                PullFolder = inputFolder;
            }

            Console.WriteLine("ensure command (y/n):");
            Console.WriteLine($"{type}@{inputRemoteDevice}:{inputFolder}");

            while (true)
            {
                char keyChar = Console.ReadKey().KeyChar;
                if (keyChar == 'y')
                {
                    Console.WriteLine();
                    return;
                }
                else if (keyChar == 'n')
                    throw new Exception("User Cancel");
                else
                    Console.WriteLine("\r \r");
            }
        }
    }
}