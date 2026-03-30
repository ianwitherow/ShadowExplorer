# Shadow Explorer

A Windows utility that adds **"View Shadow Copies"** to the right-click context menu in Explorer, letting you browse and restore previous versions of files from Volume Shadow Copy (VSS) snapshots.

![Shadow Explorer](https://img.shields.io/badge/platform-Windows-blue) ![.NET 9](https://img.shields.io/badge/.NET-9.0-purple)

## Features

- **Right-click integration** - "View Shadow Copies" appears on files, folders, and folder backgrounds
- **File version history** - See all VSS snapshots of a file with dates, sizes, and a live preview pane
- **Folder history** - Browse all files and subfolders that existed across snapshots, including deleted items shown in red
- **Drag & drop** - Drag any version from the list into Explorer to copy it (auto-appends timestamp to filename)
- **Copy / Open / Save As / Restore** - Via right-click menu or bottom action buttons
- **Restore with safety net** - Creates a `.bak` backup before overwriting the current file
- **Navigate freely** - Editable path bar, up-a-level button, double-click folders to drill in
- **Dark themed UI** - Custom styled context menus, column headers, and controls

## Installation

1. Download `ShadowExplorer-Setup-x.x.x.exe` from the [latest release](https://github.com/ianwitherow/ShadowExplorer/releases/latest)
2. Run the installer

Right-click any file or folder in Explorer and select **"View Shadow Copies"**.

> **Windows 11 note:** The option appears under **"Show more options"** in the context menu.

## Uninstalling

**Settings > Apps > Installed apps > Shadow Explorer > Uninstall**

## Building from source

Requires [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) and [Inno Setup 6](https://jrsoftware.org/isinfo.php).

```powershell
# Build everything and produce the installer
.\build.ps1
```

The version is defined in `Installer/setup.iss`. The build script reads it and passes it to both the .NET publish and the installer. Output goes to `dist/`.

To publish the app without the installer:

```bash
dotnet publish ShadowExplorer/ShadowExplorer.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

## How it works

Shadow Explorer uses Windows' Volume Shadow Copy Service (VSS) via WMI to enumerate available snapshots. It creates temporary directory symlinks to shadow copy volumes, then accesses files through those symlinks. A `cmd.exe` fallback layer handles cases where .NET's file APIs can't traverse the `\\?\GLOBALROOT` device paths directly.

The app requires **administrator privileges** since VSS access needs elevation.

## Releasing a new version

1. Update the version in `Installer/setup.iss` (`#define MyAppVersion`)
2. Run `.\build.ps1`
3. Commit, tag, and push:
   ```bash
   git tag v1.0.1
   git push --tags
   ```
4. Create a GitHub release from the tag and attach `dist/ShadowExplorer-Setup-x.x.x.exe`

The installer handles upgrades automatically — users just run the new installer over the existing installation.

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
Installer/
  setup.iss              # Inno Setup script (defines version, registry, wizard)
build.ps1                # Build + package script
```

## License

MIT
