using System;
using System.Runtime.InteropServices;

namespace ViStart.Native
{
    public class KeyboardHook
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn,
            IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private LowLevelKeyboardProc proc;
        private IntPtr hookID = IntPtr.Zero;

        public event EventHandler LeftWindowsKeyPressed;
        public event EventHandler RightWindowsKeyPressed;

        public bool HookLeftWindowsKey { get; set; }
        public bool HookRightWindowsKey { get; set; }

        public KeyboardHook()
        {
            proc = HookCallback;
            HookLeftWindowsKey = true;
            HookRightWindowsKey = true;
        }

        public void Install()
        {
            if (hookID != IntPtr.Zero)
                return;

            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                hookID = SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        public void Uninstall()
        {
            if (hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hookID);
                hookID = IntPtr.Zero;
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);

                if (vkCode == VK_LWIN && HookLeftWindowsKey)
                {
                    LeftWindowsKeyPressed?.Invoke(this, EventArgs.Empty);
                    return (IntPtr)1; // Suppress the key
                }
                else if (vkCode == VK_RWIN && HookRightWindowsKey)
                {
                    RightWindowsKeyPressed?.Invoke(this, EventArgs.Empty);
                    return (IntPtr)1; // Suppress the key
                }
            }

            return CallNextHookEx(hookID, nCode, wParam, lParam);
        }
    }
}
