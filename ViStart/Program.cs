using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using ViStart.Core;
using ViStart.Native;
using ViStart.UI;

namespace ViStart
{
    static class Program
    {
        private const string MUTEX_NAME = "ViStart_SingleInstance_Mutex";
        private static System.Threading.Mutex instanceMutex;
        private static KeyboardHook keyboardHook;
        private static StartButton startButton;
        private static IntPtr realStartButtonHandle = IntPtr.Zero;

        [STAThread]
        static void Main(string[] args)
        {
            // Handle command-line arguments
            if (args.Length > 0)
            {
                HandleCommandLine(args);
                return;
            }

            // Check for existing instance
            bool createdNew;
            instanceMutex = new System.Threading.Mutex(true, MUTEX_NAME, out createdNew);

            if (!createdNew)
            {
                // Another instance is already running
                return;
            }

            // Diagnostic: catch unhandled exceptions and write them to a log file
            // next to the exe so we can see what blew up.
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                LogException("AppDomain.UnhandledException", e.ExceptionObject as Exception);
            Application.ThreadException += (s, e) =>
                LogException("Application.ThreadException", e.Exception);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                // Wait for desktop to be ready
                WaitForDesktop();

                // Hide the real Windows Start button so ours can take over
                HideRealStartButton();

                // Initialize settings
                AppSettings.Load();

                // Initialize theme
                ThemeManager.Initialize();

                // Install keyboard hook to catch Windows key
                keyboardHook = new KeyboardHook();
                keyboardHook.LeftWindowsKeyPressed += OnWindowsKeyPressed;
                keyboardHook.RightWindowsKeyPressed += OnWindowsKeyPressed;
                
                if (AppSettings.Instance.CatchLeftWindowsKey)
                    keyboardHook.HookLeftWindowsKey = true;
                if (AppSettings.Instance.CatchRightWindowsKey)
                    keyboardHook.HookRightWindowsKey = true;

                keyboardHook.Install();

                // Create and show start button
                startButton = new StartButton();
                startButton.Show();

                // Run message loop
                Application.Run();
            }
            finally
            {
                Cleanup();
            }
        }

        private static void OnWindowsKeyPressed(object sender, EventArgs e)
        {
            startButton?.ToggleStartMenu();
        }

        private static void WaitForDesktop()
        {
            // Wait for taskbar to be ready
            IntPtr taskbar = IntPtr.Zero;
            int attempts = 0;
            const int maxAttempts = 20;

            while (attempts < maxAttempts)
            {
                taskbar = User32.FindWindow("Shell_TrayWnd", null);
                if (taskbar != IntPtr.Zero && User32.IsWindowVisible(taskbar))
                {
                    break;
                }
                System.Threading.Thread.Sleep(500);
                attempts++;
            }
        }

        private static void HandleCommandLine(string[] args)
        {
            // Future: handle theme installation, etc.
        }

        private static void HideRealStartButton()
        {
            IntPtr taskbar = User32.FindWindow("Shell_TrayWnd", null);
            if (taskbar == IntPtr.Zero)
                return;

            // Classic XP/Win7 start button: child Button class, text "Start"
            realStartButtonHandle = User32.FindWindowEx(taskbar, IntPtr.Zero, "Button", "Start");
            if (realStartButtonHandle == IntPtr.Zero)
            {
                // Some Win7+ skins use a different class; try the "Start" class as a fallback
                realStartButtonHandle = User32.FindWindowEx(taskbar, IntPtr.Zero, "Start", null);
            }

            if (realStartButtonHandle != IntPtr.Zero)
                User32.ShowWindow(realStartButtonHandle, User32.SW_HIDE);
        }

        private static void RestoreRealStartButton()
        {
            if (realStartButtonHandle != IntPtr.Zero)
                User32.ShowWindow(realStartButtonHandle, User32.SW_SHOW);
        }

        private static void Cleanup()
        {
            keyboardHook?.Uninstall();
            AppSettings.Save();
            RestoreRealStartButton();
            try { instanceMutex?.ReleaseMutex(); } catch { }
        }

        public static void Exit()
        {
            Cleanup();
            Application.Exit();
        }

        public static void Log(string message)
        {
            try
            {
                string logPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "vistart-debug.log");
                System.IO.File.AppendAllText(logPath,
                    DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message + System.Environment.NewLine);
            }
            catch { }
        }

        private static void LogException(string source, Exception ex)
        {
            if (ex == null) Log(source + ": (null exception)");
            else Log(source + ": " + ex.GetType().Name + ": " + ex.Message + System.Environment.NewLine + ex.StackTrace);
        }
    }
}
