using System.IO;
using System.Text.Json;
using TaskbarFolders.Models;

namespace TaskbarFolders.Services;

public static class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string ConfigDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TaskbarFolders");

    public static string ConfigPath { get; } = Path.Combine(ConfigDirectory, "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                var defaults = CreateDefault();
                Save(defaults);
                return defaults;
            }

            var json = File.ReadAllText(ConfigPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
            return config ?? CreateDefault();
        }
        catch
        {
            return CreateDefault();
        }
    }

    public static void Save(AppConfig config)
    {
        Directory.CreateDirectory(ConfigDirectory);
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }

    private static AppConfig CreateDefault()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");

        var config = new AppConfig();

        if (Directory.Exists(desktop))
            config.Folders.Add(new FolderEntry { Path = desktop, DisplayName = "Desktop" });
        if (Directory.Exists(documents))
            config.Folders.Add(new FolderEntry { Path = documents, DisplayName = "Documents" });
        if (Directory.Exists(downloads))
            config.Folders.Add(new FolderEntry { Path = downloads, DisplayName = "Downloads" });

        return config;
    }
}
