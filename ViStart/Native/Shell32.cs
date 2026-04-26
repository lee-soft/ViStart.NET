using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ViStart.Native
{
    public static class Shell32
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern uint ExtractIconEx(string szFileName, int nIconIndex,
            out IntPtr phiconLarge, IntPtr phiconSmall, uint nIcons);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern uint ExtractIconEx(string szFileName, int nIconIndex,
            IntPtr phiconLarge, out IntPtr phiconSmall, uint nIcons);

        [DllImport("shell32.dll")]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
            ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        // PIDL overload: required for icons of virtual shell items (e.g. shell:AppsFolder
        // entries) that have no filesystem path Icon.ExtractAssociatedIcon could resolve.
        [DllImport("shell32.dll")]
        public static extern IntPtr SHGetFileInfo(IntPtr pidl, uint dwFileAttributes,
            ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHParseDisplayName(string pszName, IntPtr pbc,
            out IntPtr ppidl, uint sfgaoIn, out uint psfgaoOut);

        [DllImport("shell32.dll")]
        public static extern void ILFree(IntPtr pidl);

        // SHCreateItemFromParsingName accepts both filesystem paths and shell parsing
        // names (including "shell:..."), making it the right entry point for resolving
        // virtual shell items like AppsFolder/Microsoft Store entries.
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHCreateItemFromParsingName(string pszPath, IntPtr pbc,
            [In] ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);

        // Extract the absolute PIDL out of any shell COM object (e.g. a Shell.Application
        // FolderItem). This lets us reuse the exact item the shell enumerated, which carries
        // richer icon bindings than what SHCreateItemFromParsingName recreates from text.
        [DllImport("shell32.dll", PreserveSig = false)]
        public static extern void SHGetIDListFromObject(
            [MarshalAs(UnmanagedType.IUnknown)] object iUnknown,
            out IntPtr ppidl);

        [DllImport("shell32.dll", PreserveSig = false)]
        public static extern void SHCreateItemFromIDList(IntPtr pidl,
            [In] ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);

        [ComImport]
        [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IShellItem
        {
            void BindToHandler(IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName(uint sigdnName, out IntPtr ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }

        [ComImport]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IShellItemImageFactory
        {
            // HBITMAP returned is a 32bpp ARGB premultiplied DIB section sized as requested.
            void GetImage([In] SIZE size, [In] int flags, out IntPtr phbm);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE
        {
            public int cx;
            public int cy;
        }

        // Flags for IShellItemImageFactory::GetImage.
        public const int SIIGBF_RESIZETOFIT  = 0x00;
        public const int SIIGBF_BIGGERSIZEOK = 0x01;
        public const int SIIGBF_MEMORYONLY   = 0x02;
        public const int SIIGBF_ICONONLY     = 0x04;  // Skip thumbnails — we want the icon.
        public const int SIIGBF_THUMBNAILONLY= 0x08;
        public const int SIIGBF_INCACHEONLY  = 0x10;
        public const int SIIGBF_ICONBACKGROUND = 0x80;

        [DllImport("shell32.dll")]
        public static extern int SHGetSpecialFolderLocation(IntPtr hwndOwner, int nFolder,
            ref IntPtr ppidl);

        [DllImport("shell32.dll")]
        public static extern bool SHGetPathFromIDList(IntPtr pidl, StringBuilder pszPath);

        // Structures
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        // shell32 ordinal #261 — undocumented but stable since Vista. Returns the
        // path to the current user's account-tile image (the photo shown next to
        // the username in the start menu). Used by VB6 ViStart's MakeUserRollover.
        // Signature: HRESULT (LPCWSTR username, DWORD flags, LPWSTR buffer, DWORD cch).
        // Pass empty username + 0x80000000 to mean "current user".
        [DllImport("shell32.dll", EntryPoint = "#261", CharSet = CharSet.Unicode, PreserveSig = true)]
        public static extern int GetUserTilePath(string username, uint flags,
            System.Text.StringBuilder buffer, int cchBuffer);

        // Constants
        public const uint SHGFI_ICON = 0x100;
        public const uint SHGFI_LARGEICON = 0x0;
        public const uint SHGFI_SMALLICON = 0x1;
        public const uint SHGFI_USEFILEATTRIBUTES = 0x10;
        public const uint SHGFI_PIDL = 0x000000008;

        public const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
    }
}
