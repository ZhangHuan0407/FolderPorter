using FolderPorter.Model;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static System.Net.Mime.MediaTypeNames;

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
                Directory.CreateDirectory(directoryPath, unixFileMode);
            else
                Directory.CreateDirectory(directoryPath);
        }

        public static void LetHeadLinkToDirectory(string headDirectoryPath, string directoryPath)
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT &&
                !SystemPermission.IsWindowsAdministrator())
            {
                ConsoleColor color = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Current user is not administrator, If you use VersionControl, app can't create Head directory link.");
                Console.ForegroundColor = color;
                return;
            }
            DirectoryInfo directoryInfo = new DirectoryInfo(headDirectoryPath);
            if (directoryInfo.Exists && !string.IsNullOrEmpty(directoryInfo.LinkTarget))
                directoryInfo.Delete();
            Directory.CreateSymbolicLink(headDirectoryPath, directoryPath);
            directoryInfo.Refresh();
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                directoryInfo.UnixFileMode = Program.DirectoryUnixFileMode;
        }

        public static void SetFileMode(FileInfo fileInfo, UnixFileMode fileUnixFileMode)
        {
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                fileInfo.UnixFileMode = fileUnixFileMode;
        }

        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern bool CreateHardLink(
            string lpFileName,
            string lpExistingFileName,
            IntPtr lpSecurityAttributes
        );
        public static void CopyFileOrHardLink(FileInfo sourceFileInfo, FileInfo targetFileInfo)
        {
            CreateDirectory(targetFileInfo.DirectoryName!, Program.DirectoryUnixFileMode);
            if (AppSettingModel.Instance.HardLinkInsteadOfCopy &&
                Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                if (!CreateHardLink(targetFileInfo.FullName, sourceFileInfo.FullName, IntPtr.Zero))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            else if (AppSettingModel.Instance.HardLinkInsteadOfCopy &&
                OperatingSystem.IsLinux())
                Mono.Unix.Native.Syscall.link(sourceFileInfo.FullName, targetFileInfo.FullName);
            else
                File.Copy(sourceFileInfo.FullName, targetFileInfo.FullName);
            SetFileMode(targetFileInfo, Program.FileUnixFileMode);
        }
    }
}