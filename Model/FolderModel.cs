using System;
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
        public string? Version => m_VersionControlModel?.Version;
        public string? LastSuccessVersion => m_VersionControlModel?.LastSuccessVersion;

        [JsonIgnore]
        public string Folder { get; set; }

        private byte[]? m_Buffer;

        public FolderModel()
        {
        }

        public FileInfo ConvertToLastSuccessFileInfo(string fileRelativePath) =>
            ConvertToFileInfo(fileRelativePath, m_VersionControlModel?.LastSuccessVersion ?? string.Empty);

        public FileInfo ConvertToCurrentFileInfo(string fileRelativePath) =>
            ConvertToFileInfo(fileRelativePath, m_VersionControlModel?.Version ?? string.Empty);

        private FileInfo ConvertToFileInfo(string fileRelativePath, string version)
        {
            string directoryPath = ConvertToDirectoryPath(version);
            string fileFullPath = $"{directoryPath}{fileRelativePath}";
            FileInfo fileInfo = new FileInfo(fileFullPath);
            fileFullPath = fileInfo.FullName.Replace('\\', '/');
            if (!fileFullPath.StartsWith(directoryPath))
                throw new Exception($"FileRelativePath: {fileRelativePath}, FileFullPath: {fileFullPath}");
            return fileInfo;
        }

        public string ConvertToDirectoryPath(string version)
        {
            string directoryPath;
            if (VersionControl && !string.IsNullOrEmpty(version))
                directoryPath = $"{RootPath}/{version[..8]}/";
            else
                directoryPath = $"{RootPath}/";
            return directoryPath;
        }

        public void CopyFileFromOldVersion(string fileRelativePath, long trimFileLength)
        {
            if (!VersionControl || m_VersionControlModel == null)
                return;
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

        public IEnumerable<(string fileRelativePath, FileInfo fileInfo)> EnumFiles(string? version = null)
        {
            if (string.IsNullOrEmpty(version))
                version = m_VersionControlModel?.LastSuccessVersion;
            string prefixStr = ConvertToDirectoryPath(version ?? string.Empty);

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
                string fileRelativePath = filePath.Substring(prefixStr.Length);
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
            if (VersionControl)
                return;
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

        public void CheckAndRemoveInvalidVersion(string? version)
        {
            if (!VersionControl || m_VersionControlModel == null)
                return;
            if (string.IsNullOrEmpty(version))
                return;
            // last version is valid
            if (m_VersionControlModel.ValidVersionList.Any(entry => entry.Version == version))
                return;
            Console.WriteLine($"Remove invalid Version: {version} in Folder {Folder}.");

            string directoryPath = ConvertToDirectoryPath(m_VersionControlModel.Version);
            if (Directory.Exists(directoryPath))
                Directory.Delete(directoryPath, true);
        }

        public void StartNewVersion(string remoteUser, EndPoint? remoteEndPoint)
        {
            if (VersionControl &&
                m_VersionControlModel != null)
            {
                m_VersionControlModel.Version = Guid.NewGuid().ToString().Replace("-", string.Empty);
                Console.WriteLine($"StartNewVersion, Version: {m_VersionControlModel.Version}, {DateTime.Now}");
                Console.WriteLine($"RemoteUser: {remoteUser} {remoteEndPoint}");
                SystemIOAPI.CreateDirectory(ConvertToDirectoryPath(m_VersionControlModel.Version), Program.DirectoryUnixFileMode);
                SaveVersionControl();
            }
        }

        public void SetVersionResult(string remoteUser, bool anyNewModifyOrDelete)
        {
            if (!VersionControl || m_VersionControlModel == null)
                return;
            Console.WriteLine($"SaveFinishVersion, Version: {m_VersionControlModel.Version}, {DateTime.Now}");
            if (anyNewModifyOrDelete)
            {
                m_VersionControlModel.LastSuccessVersion = m_VersionControlModel.Version;
                ValidVersionEntry validVersionEntry = new ValidVersionEntry(m_VersionControlModel.Version, DateTime.Now, remoteUser);
                m_VersionControlModel.ValidVersionList.Insert(0, validVersionEntry);
            }
            else
                m_VersionControlModel.Version = m_VersionControlModel.LastSuccessVersion;
            SaveVersionControl();
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