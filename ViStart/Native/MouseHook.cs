using System;
using System.Runtime.InteropServices;

namespace ViStart.Native
{
    public class MouseHookEventArgs : EventArgs
    {
        public int X;
        public int Y;
    }

    /// <summary>
    /// Low-level (WH_MOUSE_LL) mouse hook. Used to dismiss the start menu when
    /// the user clicks outside it — Form.Deactivate is unreliable on a layered
    /// window with WS_EX_NOACTIVATE.
    /// </summary>
    public class MouseHook
    {
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn,
            IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private LowLevelMouseProc proc;
        private IntPtr hookID = IntPtr.Zero;

        public event EventHandler<MouseHookEventArgs> LeftButtonDown;
        public event EventHandler<MouseHookEventArgs> RightButtonDown;

        public MouseHook()
        {
            proc = HookCallback;
        }

        public void Install()
        {
            if (hookID != IntPtr.Zero)
                return;

            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                hookID = SetWindowsHookEx(WH_MOUSE_LL, proc,
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
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                if (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN)
                {
                    var data = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                    var args = new MouseHookEventArgs { X = data.pt.x, Y = data.pt.y };
                    if (msg == WM_LBUTTONDOWN)
                        LeftButtonDown?.Invoke(this, args);
                    else
                        RightButtonDown?.Invoke(this, args);
                }
            }

            return CallNextHookEx(hookID, nCode, wParam, lParam);
        }
    }
}
