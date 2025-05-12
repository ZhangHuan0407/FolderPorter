using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FolderPorter.Model
{
    [Serializable]
    public class FolderModel
    {
        public string RootPath { get; set; }
        public bool CanWrite { get; set; }
        public bool CanRead { get; set; }
        public bool VersionControl { get; set; }

        private string VersionControlFilePath => $"{RootPath}/{Program.VersionControlFile}";
        private VersionControlModel? m_VersionControlModel;

        [JsonIgnore]
        public string Folder { get; set; }

        private byte[]? m_Buffer;

        public FolderModel()
        {
        }

        public FileInfo ConvertToLastSuccessFileInfo(string fileRelativePath) => ConvertToFileInfo(fileRelativePath, true);
        public FileInfo ConvertToCurrentFileInfo(string fileRelativePath) => ConvertToFileInfo(fileRelativePath, false);
        private FileInfo ConvertToFileInfo(string fileRelativePath, bool lastSuccessVersion)
        {
            string directoryPath;
            if (VersionControl && lastSuccessVersion)
                directoryPath = $"{RootPath}/{m_VersionControlModel!.LastSuccessVersion}/";
            else if (VersionControl && !lastSuccessVersion)
                directoryPath = $"{RootPath}/{m_VersionControlModel!.Version}/";
            else
                directoryPath = $"{RootPath}/";
            string fileFullPath = $"{directoryPath}{fileRelativePath}";
            FileInfo fileInfo = new FileInfo(fileFullPath);
            fileFullPath = fileInfo.FullName.Replace('\\', '/');
            if (!fileFullPath.StartsWith(directoryPath))
                throw new Exception($"FileRelativePath: {fileRelativePath}, FileFullPath: {fileFullPath}");
            return fileInfo;
        }

        public void CopyFileFromOldVersion(string fileRelativePath, long trimFileLength)
        {
            if (VersionControl &&
                m_VersionControlModel != null)
            {
                FileInfo lastFileInfo = ConvertToLastSuccessFileInfo(fileRelativePath);
                if (!lastFileInfo.Exists)
                    return;
                FileInfo currentFileInfo = ConvertToCurrentFileInfo(fileRelativePath);
                SystemIOAPI.CreateDirectory(currentFileInfo.DirectoryName!, Program.DirectoryUnixFileMode);
                if (lastFileInfo.Length <= trimFileLength)
                {
                    File.Copy(lastFileInfo.FullName, currentFileInfo.FullName, true);
                    SystemIOAPI.SetFileMode(currentFileInfo, Program.FileUnixFileMode);
                    return;
                }
                m_Buffer ??= new byte[4096 * 10];
                using (FileStream lastFileStream = lastFileInfo.OpenRead())
                {
                    using (FileStream currentFileStream = new FileStream(currentFileInfo.FullName, FileMode.Create, FileAccess.Write))
                    {
                        while (lastFileStream.Position < lastFileStream.Length &&
                            lastFileStream.Position < trimFileLength)
                        {
                            int readBytesCount = lastFileStream.Read(m_Buffer, 0, m_Buffer.Length);
                            currentFileStream.Write(m_Buffer, 0, readBytesCount);
                        }
                        currentFileStream.SetLength(trimFileLength);
                    }
                }
                SystemIOAPI.SetFileMode(currentFileInfo, Program.FileUnixFileMode);
            }
        }

        public IEnumerable<(string fileRelativePath, FileInfo fileInfo)> EnumFiles()
        {
            DirectoryInfo directoryInfo;
            if (VersionControl)
                directoryInfo = new DirectoryInfo($"{RootPath}/{m_VersionControlModel!.LastSuccessVersion}");
            else
                directoryInfo = new DirectoryInfo(RootPath);
            string prefixStr = directoryInfo.FullName.Replace("\\", "/");

            if (!Directory.Exists(prefixStr))
                yield break;

            string[] filePathList = Directory.GetFiles(prefixStr, "*", new EnumerationOptions()
            {
                RecurseSubdirectories = true,
                MaxRecursionDepth = 200,
            });
            for (int i = 0; i < filePathList.Length; i++)
            {
                string filePath = filePathList[i].Replace("\\", "/");
                FileInfo fileInfo = new FileInfo(filePath);
                string fileRelativePath = filePath.Substring(prefixStr.Length + 1);
                yield return (fileRelativePath, fileInfo);
            }
        }

        public int CleanFifthWheelFiles(HashSet<string> fileRelativePathSet)
        {
            if (VersionControl)
                throw new InvalidOperationException($"CleanFifthWheelFiles, VersionControl: {VersionControl}");
            int deleteFilesCount = 0;
            foreach ((string fileRelativePath, FileInfo fileInfo) in EnumFiles())
            {
                if (fileRelativePathSet.Contains(fileRelativePath))
                    continue;
                fileInfo.Delete();
                deleteFilesCount++;
            }
            return deleteFilesCount;
        }

        public void CleanEmptyDirectory()
        {
            string[] subDirectories = Directory.GetDirectories(RootPath);
            for (int i = 0; i < subDirectories.Length; i++)
                CleanEmptyDirectory_Internal(subDirectories[i]);
        }
        private bool CleanEmptyDirectory_Internal(string directoryPath)
        {
            string[] subDirectories = Directory.GetDirectories(directoryPath);
            if (subDirectories.Length == 0 &&
                Directory.GetFiles(directoryPath).Length == 0)
            {
                Directory.Delete(directoryPath);
                return true;
            }
            bool haveAny = false;
            for (int i = 0; i < subDirectories.Length; i++)
            {
                if (!CleanEmptyDirectory_Internal(subDirectories[i]))
                    haveAny = true;
            }
            if (!haveAny &&
                Directory.GetFiles(directoryPath).Length == 0)
            {
                Directory.Delete(directoryPath);
                return true;
            }
            return false;
        }

        public void LoadVersionControl()
        {
            if (!VersionControl)
            {
                m_VersionControlModel = null;
                return;
            }
            if (!File.Exists(VersionControlFilePath))
            {
                Console.WriteLine($"Not found {Program.VersionControlFile}, create new one.");
                m_VersionControlModel = new VersionControlModel();
            }
            else
            {
                string versionControlStr = File.ReadAllText(VersionControlFilePath);
                m_VersionControlModel = JsonSerializer.Deserialize<VersionControlModel>(versionControlStr)!;
            }
        }

        public void StartNewVersion(string remoteUser, EndPoint? remoteEndPoint)
        {
            if (VersionControl &&
                m_VersionControlModel != null)
            {
                m_VersionControlModel.Version++;
                Console.WriteLine($"start {m_VersionControlModel.Version} {DateTime.Now} {remoteUser} {remoteEndPoint}");
                SystemIOAPI.CreateDirectory($"{RootPath}/{m_VersionControlModel.Version}", Program.DirectoryUnixFileMode);
                SaveVersionControl();
            }
        }

        public void SaveFinishVersion(string remoteUser)
        {
            if (VersionControl &&
                m_VersionControlModel != null)
            {
                Console.WriteLine($"finish {m_VersionControlModel.Version} {DateTime.Now}");
                m_VersionControlModel.LastSuccessVersion = m_VersionControlModel.Version;
                ValidVersionEntry validVersionEntry = new ValidVersionEntry(m_VersionControlModel.Version, DateTime.Now, remoteUser);
                m_VersionControlModel.ValidVersionList.Add(validVersionEntry);
                SaveVersionControl();
            }
            m_Buffer = null;
        }
        public void SaveVersionControl()
        {
            if (VersionControl &&
                m_VersionControlModel != null)
            {
                JsonSerializerOptions options = new JsonSerializerOptions()
                {
                    WriteIndented = true,
                };
                string versionControlStr = JsonSerializer.Serialize(m_VersionControlModel, options);
                File.WriteAllText(VersionControlFilePath, versionControlStr);
            }
        }

        public IReadOnlyList<ValidVersionEntry> GetValidVersionList() => m_VersionControlModel!.ValidVersionList;
    }
}