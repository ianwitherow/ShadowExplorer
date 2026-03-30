# Shadow Explorer

A Windows utility that adds **"View Shadow Copies"** to the right-click context menu in Explorer, letting you browse and restore previous versions of files from Volume Shadow Copy (VSS) snapshots.

![Shadow Explorer](https://img.shields.io/badge/platform-Windows-blue) ![.NET 9](https://img.shields.io/badge/.NET-9.0-purple)

## Features

- **Right-click integration** - "View Shadow Copies" appears on files, folders, and folder backgrounds
- **File version history** - See all VSS snapshots of a file with dates, sizes, and a live preview pane
- **Folder history** - Browse all files (and subfolders) that existed across snapshots, including deleted items shown in red
- **Drag & drop** - Drag any version from the list into Explorer to copy it (auto-appends timestamp to filename)
- **Copy / Open / Save As / Restore** - Via right-click menu or bottom action buttons
- **Restore with safety net** - Creates a `.bak` backup before overwriting the current file
- **Navigate freely** - Editable path bar, up-a-level button, double-click folders to drill in
- **Dark themed UI** - Custom styled context menus, column headers, and controls

## Installation

1. Download the latest release (3 files: `setup.exe`, `ShadowExplorer.exe`, `uninstall.exe`)
2. Place all three files in the same folder
3. Run `setup.exe`
4. Click **Yes** at the prompt

That's it. Right-click any file or folder in Explorer and select **"View Shadow Copies"**.

> **Windows 11 note:** The option appears under **"Show more options"** in the context menu.

## Uninstalling

Either:
- Go to **Settings > Apps > Installed apps** and uninstall "Shadow Explorer"
- Or run `uninstall.exe` from `C:\Program Files\ShadowExplorer\`

## Building from source

Requires [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

```bash
# Build
dotnet build -c Release

# Publish self-contained executables
dotnet publish ShadowExplorer/ShadowExplorer.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true

dotnet publish Setup/Setup.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true

dotnet publish Uninstall/Uninstall.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

The three `.exe` files will be in each project's `bin/Release/net9.0-windows/win-x64/publish/` folder.

## How it works

Shadow Explorer uses Windows' Volume Shadow Copy Service (VSS) via WMI to enumerate available snapshots. It creates temporary directory symlinks to shadow copy volumes, then accesses files through those symlinks. A `cmd.exe` fallback layer handles cases where .NET's file APIs can't traverse the `\\?\GLOBALROOT` device paths directly.

The app requires **administrator privileges** since VSS access needs elevation.

## Project structure

```
ShadowExplorer/          # Main WPF application
  Services/
    ShadowCopyService.cs # VSS enumeration, mounting, file operations
    FolderPicker.cs      # COM-based folder picker (no WinForms dependency)
  Models/
    ShadowCopyInfo.cs    # Data models for versions and folder entries
  Converters/
    Converters.cs        # WPF value converters for the dark theme
  MainWindow.xaml/.cs    # Main UI
  App.xaml/.cs           # Application entry point
Setup/                   # Installer (copies files + registers context menu)
Uninstall/               # Uninstaller (removes registry + deletes files)
```

## License

MIT
