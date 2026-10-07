using System.Text.Json;

namespace MangaDl.Services;

public sealed class AppSettings
{
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "manga-dl");

    private static readonly string SettingsFile = Path.Combine(SettingsFolder, "settings.json");

    public string AccentColor { get; set; } = "Red";
    public string ReaderMode { get; set; } = "Spread"; // "Spread" | "Single" | "Webtoon"
    public string ReadingDirection { get; set; } = "RTL"; // "RTL" | "LTR"
    public string DownloadPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Downloads",
        "manga-dl");
    public string? BackendUrl { get; set; }
    public string? ApiKey { get; set; }
    public bool AutoCheckUpdates { get; set; } = true;

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null) return loaded;
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("AppSettings.Load", ex);
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsFolder);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFile, json);
        }
        catch (Exception ex)
        {
            AppLog.Warn("AppSettings.Save", ex);
        }
    }
}
