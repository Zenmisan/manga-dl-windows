using System.Text.Json;
using System.Text.Json.Serialization;

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

    public const string DefaultSupabaseUrl = "https://gyivwfweldwvzccbpgoz.supabase.co";
    public const string DefaultSupabaseAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Imd5aXZ3ZndlbGR3dnpjY2JwZ296Iiwicm9sZSI6ImFub24iLCJpYXQiOjE3ODAxNjg2NTUsImV4cCI6MjA5NTc0NDY1NX0.XcEJk1fyv-QxSehPUIeRR77ocIkPIZzDyc4DDfrr6XQ";

    public string? SupabaseUrl { get; set; } = DefaultSupabaseUrl;
    public string? SupabaseAnonKey { get; set; } = DefaultSupabaseAnonKey;
    public string? UserEmail { get; set; }
    public string? UserId { get; set; }
    public string? DisplayName { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }

    // AniList Tracker Settings
    public string AnilistClientId { get; set; } = "53109";
    public string AnilistClientSecret { get; set; } = "7xKnM4QU77qtWWLXOw3V0rx1mvLnmriX9ZcI3Cki";
    public string AnilistRedirectUri { get; set; } = "http://localhost:5678/mal-callback";
    public string? AnilistUsername { get; set; }
    public bool AnilistConnected { get; set; }

    // MyAnimeList Tracker Settings
    public string MalClientId { get; set; } = "4ee644500f513dd7887120b026b65f39";
    public string MalRedirectUri { get; set; } = "http://localhost:5678/mal-callback";
    public string? MalUsername { get; set; }
    public bool MalConnected { get; set; }

    // Google Sign-In (Supabase identity, not a tracker). The client secret is
    // intentionally NOT hardcoded here — GitHub's push protection recognizes and
    // blocks commits containing a raw Google OAuth client secret. Set it once via
    // the MANGADL_GOOGLE_CLIENT_SECRET environment variable (Load() below picks it
    // up) — it then lives only in this machine's settings.json (outside the repo,
    // in %LOCALAPPDATA%), never in source control.
    public string GoogleClientId { get; set; } = "912012648755-b5dv82bhre9ccsig25e7863dma4e6t7l.apps.googleusercontent.com";
    public string GoogleClientSecret { get; set; } = "";
    public string GoogleRedirectUri { get; set; } = "http://localhost:5678/google-callback";

    // Tracker Sync Options
    public bool AutoSyncTrackers { get; set; } = true;
    public bool MarkTrackerCompletedOnFinish { get; set; } = true;
    public bool MarkTrackerReadingOnFirstChapter { get; set; } = true;
    public bool PullProgressFromTrackers { get; set; } = false;

    /// <summary>Mirrors the web app's `hasSupabase` check — when unset, auth pages fall
    /// back to "local mode" (no account, straight into the app).</summary>
    [JsonIgnore]
    public bool HasSupabase => !string.IsNullOrWhiteSpace(SupabaseUrl) && !string.IsNullOrWhiteSpace(SupabaseAnonKey);

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    if (string.IsNullOrWhiteSpace(loaded.SupabaseUrl))
                        loaded.SupabaseUrl = DefaultSupabaseUrl;
                    if (string.IsNullOrWhiteSpace(loaded.SupabaseAnonKey))
                        loaded.SupabaseAnonKey = DefaultSupabaseAnonKey;
                    if (string.IsNullOrWhiteSpace(loaded.GoogleClientSecret))
                        loaded.GoogleClientSecret = Environment.GetEnvironmentVariable("MANGADL_GOOGLE_CLIENT_SECRET") ?? "";
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("AppSettings.Load", ex);
        }

        var fresh = new AppSettings();
        fresh.GoogleClientSecret = Environment.GetEnvironmentVariable("MANGADL_GOOGLE_CLIENT_SECRET") ?? "";
        return fresh;
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
