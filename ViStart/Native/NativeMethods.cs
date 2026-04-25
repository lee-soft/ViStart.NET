using System;
using System.Runtime.InteropServices;

namespace ViStart.Native
{
    public static class NativeMethods
    {
        [DllImport("kernel32.dll")]
        public static extern void Sleep(uint dwMilliseconds);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges,
            ref TOKEN_PRIVILEGES NewState, uint BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out LUID lpLuid);

        // Shutdown/Logoff support
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool ExitWindowsEx(uint uFlags, uint dwReason);

        public const uint EWX_LOGOFF = 0x00000000;
        public const uint EWX_SHUTDOWN = 0x00000001;
        public const uint EWX_REBOOT = 0x00000002;
        public const uint EWX_FORCE = 0x00000004;
        public const uint EWX_POWEROFF = 0x00000008;

        public const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";
        public const uint SE_PRIVILEGE_ENABLED = 0x00000002;
        public const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        public const uint TOKEN_QUERY = 0x0008;

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            public LUID Luid;
            public uint Attributes;
        }

        public static bool EnableShutdownPrivilege()
        {
            try
            {
                IntPtr tokenHandle;
                if (!OpenProcessToken(System.Diagnostics.Process.GetCurrentProcess().Handle,
                    TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out tokenHandle))
                {
                    return false;
                }

                TOKEN_PRIVILEGES tkp;
                tkp.PrivilegeCount = 1;
                tkp.Attributes = SE_PRIVILEGE_ENABLED;

                if (!LookupPrivilegeValue(null, SE_SHUTDOWN_NAME, out tkp.Luid))
                {
                    return false;
                }

                return AdjustTokenPrivileges(tokenHandle, false, ref tkp, 0, IntPtr.Zero, IntPtr.Zero);
            }
            catch
            {
                return false;
            }
        }
    }
}
