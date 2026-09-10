# Taskbar Folders for Windows 11

Restores the Windows 10 “folder on the taskbar” toolbar experience on Windows 11.

Windows 10 let you unlock the taskbar and add a **New toolbar** pointing at a folder; clicking it showed the folder contents as a cascading menu. That feature was removed in Windows 11. This app brings it back.

## What you get

- A slim toolbar that sits just above the taskbar
- One button per pinned folder (Desktop, Documents, Downloads by default)
- Click a folder → cascading menu of files and subfolders (with icons)
- Click a file to open it, or **Open folder** to launch Explorer
- Notification-area icon with the same folders
- Settings: add/remove/reorder folders, autostart, position, opacity

## Requirements

- Windows 11 (also works on Windows 10)
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (included if you publish self-contained)

## Build & run

```powershell
cd c:\Repos\taskbarFolders
dotnet build -c Release
dotnet run -c Release
```

Publish a standalone exe:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

Then run `publish\TaskbarFolders.exe`.

## Usage

1. Start the app — the toolbar appears above the taskbar.
2. Click a folder name (e.g. **Documents »**) to browse contents.
3. Right-click the toolbar → **Add folder…** / **Settings…** / **Exit**.
4. Optional: enable **Start with Windows** in Settings.

Config is stored at `%AppData%\TaskbarFolders\config.json`.
