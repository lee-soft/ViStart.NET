using System.Drawing;

namespace ViStart.NET
{
    // Interface defining start menu operations
    public interface IStartMenuManager
    {
        void ShowStartMenu();
        void ShowStartMenu(Point orbLocation);
        void HideStartMenu();
        IconManager Icons { get; }
        Settings Settings { get; }
    }

    // Static manager for global access
    public static class GlobalManager
    {
        public static IStartMenuManager Current { get; set; }
    }
}
