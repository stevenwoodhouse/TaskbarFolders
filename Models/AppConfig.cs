using System.Text.Json.Serialization;

namespace TaskbarFolders.Models;

public sealed class AppConfig
{
    public List<FolderEntry> Folders { get; set; } = [];
    public bool StartWithWindows { get; set; }
    public bool ShowToolbar { get; set; } = true;
    public bool ShowTrayIcon { get; set; } = true;
    public int MaxItemsPerMenu { get; set; } = 40;
    public bool ShowHiddenFiles { get; set; }
    public bool ShowFileExtensions { get; set; } = true;
    public bool ShowFolderNames { get; set; } = true;
    /// <summary>0 = after task buttons, 1 = center of free space, 2 = before system tray.</summary>
    public int ToolbarAlignment { get; set; } = 2;
    public int ToolbarOffsetX { get; set; }
    /// <summary>Manual toolbar width in pixels. 0 = fit content.</summary>
    public int ToolbarWidthPx { get; set; }
}

public sealed class FolderEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Path { get; set; } = "";
    public string? DisplayName { get; set; }

    [JsonIgnore]
    public string Name =>
        string.IsNullOrWhiteSpace(DisplayName)
            ? System.IO.Path.GetFileName(Path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar))
            : DisplayName;
}
