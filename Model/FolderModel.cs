using System.Text.Json.Serialization;

namespace FolderPorter.Model
{
    [Serializable]
    public class FolderModel
    {
        public string RootPath { get; set; }
        public bool CanWrite { get; set; }
        public bool CanRead { get; set; }

        [JsonIgnore]
        public string Folder { get; set; }

        public IEnumerable<(string fileRelativePath, FileInfo fileInfo)> EnumFiles()
        {
            string prefixStr = new DirectoryInfo(RootPath).FullName.Replace("\\", "/");

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
            string[] subDirectories = Directory.GetDirectories(directoryPath);
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
    }
}