using System.Security.Principal;
using System.Runtime.InteropServices;

namespace FolderPorter
{
    public static class SystemPermission
    {
        public static bool IsWindowsAdministrator()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return false;
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
    }
}