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

    // Reader Settings
    public string ReaderMode { get; set; } = "Right to left"; // "Left to right" | "Right to left" | "Vertical" | "Webtoon"
    public string ReadingDirection { get; set; } = "RTL"; // "RTL" | "LTR"
    public bool TwoPageSpread { get; set; } = true;
    public bool CropBorders { get; set; } = false;
    public int WebtoonSidePadding { get; set; } = 15; // 0 - 40 %
    public string PageFit { get; set; } = "Fit height"; // "Fit height" | "Fit width" | "Original"
    public string ReaderBackground { get; set; } = "Black"; // "Black" | "Gray" | "White"
    public bool ShowPageNumber { get; set; } = true;
    public bool OpenFullscreen { get; set; } = false;
    public bool ClickZones { get; set; } = true;
    public string MouseWheelAction { get; set; } = "Scroll"; // "Scroll" | "Turn page" | "Zoom"
    public int ImageBrightness { get; set; } = 0; // -50 - 50
    public bool ImageGrayscale { get; set; } = false;
    public bool ImageInvert { get; set; } = false;
    public bool SharpenImages { get; set; } = true;
    public string DownloadPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Downloads",
        "manga-dl");
    public const string DefaultBackendUrl = "https://manga-dl.onrender.com";

    /// <summary>Defaults to the real production backend — same pattern as Supabase's
    /// default below. Users only need to touch the Account Server field if they're
    /// self-hosting their own backend instead; it isn't something that should need
    /// manual entry to get cross-platform profile sync working out of the box.</summary>
    public string? BackendUrl { get; set; } = DefaultBackendUrl;
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

    // Google Sign-In (Supabase identity, not a tracker). Uses Supabase's own hosted
    // /auth/v1/authorize flow — Supabase already has a Google client id/secret
    // configured under Authentication > Providers, same one the web app's "Continue
    // with Google" button uses. Windows never needs its own Google OAuth client or
    // secret at all; it only needs somewhere to redirect back to.
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
                    return loaded;
                }
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
