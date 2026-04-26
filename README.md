# ViStart .NET

A reimplementation of the Vista-style Start Menu for Windows 7+ using .NET Framework 4.0.

## Overview

This is a complete rewrite of the original VB6 ViStart project, modernized for .NET while maintaining compatibility with Windows 7 and later versions. The project leverages built-in .NET features to significantly reduce code complexity compared to the original VB6 implementation.

## Key Improvements Over VB6 Version

### Architecture
- **Modern .NET Framework**: Uses .NET 4.0 for broad compatibility
- **Native GDI+ Support**: No need for custom GDI+ wrappers
- **Built-in Layered Windows**: Uses .NET's native support for alpha-blended windows
- **JSON Configuration**: Simple, human-readable settings storage
- **Strong Typing**: Full type safety throughout the codebase

### Code Reduction
- **~80% Less Code**: From ~15,000+ lines to ~3,000 lines
- **No Custom Collections**: Uses generic List<T> and Dictionary<K,V>
- **No Manual Memory Management**: Automatic garbage collection
- **Simplified Graphics**: Uses System.Drawing instead of custom wrappers
- **Built-in XML Support**: Uses System.Xml.Linq for theme parsing

### Features
- Custom Start Button (orb) with hover states
- Layered, alpha-blended Start Menu
- Pinned and Frequent Programs
- All Programs menu with search
- Search functionality with live results
- Power options (Shutdown, Restart, Log Off)
- Navigation pane for common folders
- Theme support via layout.xml
- Keyboard hook for Windows key capture
- Settings persistence in JSON format

## Project Structure

```
ViStart/
├── Core/                   # Core functionality
│   ├── AppSettings.cs      # JSON-based settings management
│   ├── ThemeManager.cs     # Theme loading and parsing
│   ├── IconCache.cs        # Icon caching system
│   └── StartMenuIndexer.cs # Program indexing and search
├── Data/                   # Data models
│   ├── Program.cs          # Program representation
│   ├── ProgramDatabase.cs  # Program storage
│   └── NavigationItem.cs   # Navigation items
├── Native/                 # P/Invoke declarations
│   ├── User32.cs           # User32.dll functions
│   ├── Shell32.cs          # Shell32.dll functions
│   ├── Gdi32.cs            # GDI32.dll functions
│   ├── KeyboardHook.cs     # Low-level keyboard hook
│   └── NativeMethods.cs    # Misc native methods
├── UI/                     # User interface components
│   ├── LayeredWindow.cs    # Base layered window class
│   ├── StartButton.cs      # Start button/orb
│   ├── StartMenu.cs        # Main start menu window
│   ├── FrequentProgramsPanel.cs
│   ├── ProgramMenuPanel.cs
│   ├── SearchBox.cs
│   ├── PowerButton.cs
│   └── JumpListPanel.cs
└── Program.cs              # Application entry point
```

## Configuration

### Settings Location
Settings are stored in JSON format at:
`%APPDATA%\Lee-Soft.com\ViStart\settings.json`

Current JSON settings include values like `CurrentSkin`, `CurrentOrb`, keyboard hook options,
and visual behavior flags (see `AppSettings.cs`).

### Theme Structure
Themes are defined in `layout.xml` files that specify element positions, colors, and images.

### Skin Support
Skins can be placed in:
`%APPDATA%\Lee-Soft.com\ViStart\_skins\[SkinName]\`

Each skin folder should contain:
- `startmenu.png` - Main menu background
- `start_button.png` - Start button/orb (4 vertical states)
- `layout.xml` - Element positioning
- Additional graphics as needed

Right-click the ViStart orb to open a quick menu where you can switch the active skin and orb.
Selected values are persisted in `settings.json`.


### Language Support
Runtime UI language switching is supported via JSON language packs in `Languages/*.json`
(default `english.json`; includes `czech`, `brazilian`, `chinesesimplified`, `dutch`, `finnish`, `french`, `german`, `hebrew`, `italian`, `korean`, `polish`, `romanian`, `russian`, `spanish`, `thai`, `turkish`). Language packs are maintained directly as JSON files in this repository.

## Building

Requirements:
- Visual Studio 2010 or later
- .NET Framework 4.0 or later

Build the solution:
```
msbuild ViStart.sln /p:Configuration=Release
```

Or open `ViStart.sln` in Visual Studio and build from the IDE.

## Compatibility

- **Minimum**: Windows 7
- **Tested**: Windows 7, Windows 8.1, Windows 10, Windows 11
- **Architecture**: x86 and x64 build configurations are available
- **.NET**: Requires .NET Framework 4.0 Client Profile

## Migration from VB6 Version

This version does **not** read old VB6 settings files. This was an intentional design decision to start with a clean slate and modern data format (JSON).

If you have the VB6 version installed, both can coexist, but they maintain separate settings.

## Features Removed from VB6 Version

The following features were intentionally removed to simplify the codebase:
- Windows 8 Metro integration
- Plugin system
- Command-line skin/orb installation
- Multiple layout support in single XML
- Advanced font configuration
- VML document support
- Automatic updates

## Development Notes

### Why .NET 4.0?
.NET 4.0 was chosen as it's the last version that supports Windows XP SP3 (though Windows 7 is our minimum target). It's also widely available on Windows 7+ systems without requiring additional downloads.

### Architecture Decisions
1. **Layered Windows**: All windows use the layered window API for proper alpha blending
2. **Manual Rendering**: Controls are rendered manually to bitmaps for full control over appearance
3. **Event-Driven**: Uses standard .NET event model instead of VB6's WithEvents
4. **Separation of Concerns**: Clear separation between UI, data, and business logic

### Performance
The .NET version is significantly faster than the VB6 version due to:
- Better memory management
- Efficient icon caching
- Optimized rendering pipeline
- Native .NET framework optimizations

## Known Limitations

1. **Single Instance**: Only one instance can run at a time (by design)
2. **Theme Compatibility**: Old VB6 themes need minor adjustments for the new layout parser
3. **Windows XP**: While .NET 4.0 supports XP, this build targets Windows 7+ for simplicity

## Future Enhancements

Potential additions:
- More theme options
- User-customizable keyboard shortcuts
- Recent files integration
- Enhanced search with file content
- Touch-friendly mode for tablets

## License

Same license as the original ViStart project.

## Credits

Original ViStart by Lee-Soft.com
.NET Reimplementation: 2026
