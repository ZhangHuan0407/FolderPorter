namespace FolderPorter
{
    /// <summary>
    /// Fuck you Microsoft!
    /// Directory.CreateDirectory(directoryPath, unixFileMode); directly throw exception on Windows.
    /// </summary>
    public static class SystemIOAPI
    {
        public static void CreateDirectory(DirectoryInfo directoryInfo, UnixFileMode unixFileMode) =>
            CreateDirectory(directoryInfo.FullName, unixFileMode);
        public static void CreateDirectory(string directoryPath, UnixFileMode unixFileMode)
        {
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                Directory.CreateDirectory(directoryPath, unixFileMode);
            }
            else
            {
                Directory.CreateDirectory(directoryPath);
            }
        }

        internal static void SetFileMode(FileInfo fileInfo, UnixFileMode fileUnixFileMode)
        {
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                fileInfo.UnixFileMode = fileUnixFileMode;
        }
    }
}