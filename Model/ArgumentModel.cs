using System.Text.RegularExpressions;

namespace FolderPorter.Model
{
    [Serializable]
    public class ArgumentModel
    {
        public static ArgumentModel Instance { get; set; }

        public WorkingMode Type { get; set; }

        public string RemoteDrive { get; set; }
        public string Folder { get; set; }

        public bool Help { get; set; }

        public static void ParseArgs(string[] args)
        {
            Instance = new ArgumentModel();
            Instance.Type = WorkingMode.Unknown;
            for (int i = 0; i < args.Length; i++)
            {
                string inputStr = args[i].Trim().Trim('-');
                if (WorkingMode.Server.ToString().Equals(inputStr, StringComparison.CurrentCultureIgnoreCase))
                {
                    Instance.Type = WorkingMode.Server;
                }
                else if (WorkingMode.Push.ToString().Equals(inputStr, StringComparison.CurrentCultureIgnoreCase))
                {
                    Instance.Type = WorkingMode.Push;
                    TryParseArgumentStr(args[i]);
                }
                else if (WorkingMode.Pull.ToString().Equals(inputStr, StringComparison.CurrentCultureIgnoreCase))
                {
                    Instance.Type = WorkingMode.Pull;
                    TryParseArgumentStr(args[i]);
                }
                else if (WorkingMode.List.ToString().Equals(inputStr, StringComparison.CurrentCultureIgnoreCase))
                {
                    Instance.Type = WorkingMode.List;
                    TryParseArgumentStr(args[i]);
                }
                else if (WorkingMode.Help.ToString().Equals(inputStr, StringComparison.CurrentCultureIgnoreCase))
                {
                    Instance.Help = true;
                }
            }
            Instance.Help |= args.Length == 0;

            void TryParseArgumentStr(string argumentStr)
            {
                Match match = Regex.Match(argumentStr, "@(?<RemoteDrive>[\\w_-]+):(?<Folder>[\\w_-]+)\\s?\\Z");
                if (match.Success)
                {
                    Instance.RemoteDrive = match.Groups["RemoteDrive"]?.Value ?? string.Empty;
                    Instance.Folder = match.Groups["Folder"]?.Value ?? string.Empty;
                }
                if (string.IsNullOrEmpty(Instance.RemoteDrive) ||
                    string.IsNullOrEmpty(Instance.Folder))
                    throw new Exception($"{Instance.Type} argument error");
            }
        }

        public static void LogHelp()
        {
            Console.WriteLine(@"Help: 
    server                    enable server mode and listern port appsetting.json[ListernPort]
                              app will listen for a long time and the port will not automatically exit.

    push@RemoteDrive:Folder   example: push@raspberry4:testfolder
                              Try to connect raspberry4 in appsetting.json[RemoteDevice] and
                              push all files in appsetting.json[LocalFolders][Folder] to remote folder.

    pull@RemoteDrive:Folder   example: pull@linux:folder2
                              Try to connect linux in appsetting.json[RemoteDevice] and
                              pull all files in appsetting.json[LocalFolders][Folder] from remote folder.

    list@RemoteDrive:Folder   example: list@linux:folder3
                              Try to connect linux in appsetting.json[RemoteDevice] and list all version
                              in remote folder. If remote folder not set VersionControl, will log failed.
");
        }

        public void EnterInteractiveMode()
        {
            Console.WriteLine("push, pull, list or server?");
            while (true)
            {
                string inputStr = Console.ReadLine()!.Trim().Trim('-');
                if (!Enum.TryParse<WorkingMode>(inputStr, true, out WorkingMode inputWorkingMode) ||
                    inputWorkingMode == WorkingMode.Unknown)
                    continue;
                Type = inputWorkingMode;
                break;
            }
            if (Type == WorkingMode.Server)
                return;

            Console.WriteLine("select one of remote drive (input name or number):");
            List<string> remoteDeviceList = new List<string>();
            foreach (string remoteDevice in AppSettingModel.Instance.RemoteDevice.Keys)
            {
                Console.ForegroundColor = ConsoleColor.DarkBlue;
                Console.Write($"[{remoteDeviceList.Count.ToString().PadLeft(2)}] ");
                Console.ResetColor();
                Console.WriteLine($"{remoteDevice}");
                remoteDeviceList.Add(remoteDevice);
            }
            RemoteDrive = string.Empty;
            while (true)
            {
                string input = Console.ReadLine()!.Trim();
                if (AppSettingModel.Instance.RemoteDevice.ContainsKey(input))
                {
                    RemoteDrive = input;
                    break;
                }
                if (int.TryParse(input, out int index) &&
                    index >= 0 && index < remoteDeviceList.Count)
                {
                    RemoteDrive = remoteDeviceList[index];
                    break;
                }
            }

            Console.WriteLine("select one of folder (input name or number):");
            List<string> folderList = new List<string>();
            foreach (FolderModel folderModel in AppSettingModel.Instance.LocalFolders.Values)
            {
                Console.ForegroundColor = ConsoleColor.DarkBlue;
                Console.Write($"[{folderList.Count.ToString().PadLeft(2)}] ");
                Console.Write($"{(folderModel.CanRead ? "R" : "_")}{(folderModel.CanWrite ? "W" : "_")} ");
                Console.ResetColor();
                Console.WriteLine($"{folderModel.Folder}");
                folderList.Add(folderModel.Folder);
            }
            Folder = string.Empty;
            while (true)
            {
                string input = Console.ReadLine()!.Trim();
                if (AppSettingModel.Instance.LocalFolders.ContainsKey(input))
                {
                    Folder = input;
                    break;
                }
                if (int.TryParse(input, out int index) &&
                    index >= 0 && index < folderList.Count)
                {
                    Folder = folderList[index];
                    break;
                }
            }

            Console.WriteLine("ensure command (y/n):");
            Console.WriteLine($"{Type}@{RemoteDrive}:{Folder}");

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