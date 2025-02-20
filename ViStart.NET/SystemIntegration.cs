using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ViStart.NET
{
    public static class SystemIntegration
    {
        // Win32 constants
        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;
        private const int WM_KEYDOWN = 0x0100;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;

        // Keyboard hook
        private static IntPtr keyboardHookHandle;
        private static HookProc keyboardHookProc;

        // Mouse hook
        private static IntPtr mouseHookHandle;
        private static HookProc mouseHookProc;

        // Taskbar references
        private static IntPtr taskbarHandle;
        private static IntPtr startButtonHandle;

        private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        public static void Initialize()
        {
            // Set up keyboard hook
            keyboardHookProc = KeyboardHookCallback;
            keyboardHookHandle = SetWindowsHookEx(
                WH_KEYBOARD_LL,
                keyboardHookProc,
                IntPtr.Zero,
                0);

            // Set up mouse hook
            mouseHookProc = MouseHookCallback;
            mouseHookHandle = SetWindowsHookEx(
                WH_MOUSE_LL,
                mouseHookProc,
                IntPtr.Zero,
                0);

            // Find taskbar and start button
            FindTaskbarElements();
        }

        public static void Cleanup()
        {
            // Remove hooks
            if (keyboardHookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(keyboardHookHandle);
                keyboardHookHandle = IntPtr.Zero;
            }

            if (mouseHookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(mouseHookHandle);
                mouseHookHandle = IntPtr.Zero;
            }
        }

        private static void FindTaskbarElements()
        {
            // Find the taskbar
            taskbarHandle = FindWindow("Shell_TrayWnd", null);
            if (taskbarHandle != IntPtr.Zero)
            {
                // Find the start button (may vary by Windows version)
                startButtonHandle = FindWindow("Button", "Start");
                if (startButtonHandle != IntPtr.Zero)
                {
                    // Hide the original start button
                    ShowWindow(startButtonHandle, 0);
                }
            }
        }

        private static IntPtr KeyboardHookCallback(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);

                // Handle Windows key
                if ((vkCode == VK_LWIN || vkCode == VK_RWIN) &&
                    wParam.ToInt32() == WM_KEYDOWN)
                {
                    // Trigger start menu
                    GlobalManager.Current?.ShowStartMenu();
                    return (IntPtr)1; // Handled
                }
            }

            return CallNextHookEx(keyboardHookHandle, code, wParam, lParam);
        }

        private static IntPtr MouseHookCallback(int code, IntPtr wParam, IntPtr lParam)
        {
            // Handle mouse events if needed
            return CallNextHookEx(mouseHookHandle, code, wParam, lParam);
        }

        public static bool IsStartButtonVisible
        {
            get
            {
                if (startButtonHandle == IntPtr.Zero)
                {
                    FindTaskbarElements();
                }
                return startButtonHandle != IntPtr.Zero;
            }
        }

        public static void ShowOriginalStartButton(bool show)
        {
            if (startButtonHandle != IntPtr.Zero)
            {
                ShowWindow(startButtonHandle, show ? 1 : 0);
            }
        }

        public static bool IsTaskbarAvailable
        {
            get
            {
                if (taskbarHandle == IntPtr.Zero)
                {
                    FindTaskbarElements();
                }
                return taskbarHandle != IntPtr.Zero;
            }
        }
    }

}