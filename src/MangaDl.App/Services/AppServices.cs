using MangaDl.Core.Database;
using MangaDl.Core.Downloads;
using MangaDl.Core.Extensions;
using MangaDl.Core.Http;

namespace MangaDl.Services;

public static class AppServices
{
    private static readonly Lazy<AppSettings> _settings = new(AppSettings.Load);
    public static AppSettings Settings => _settings.Value;

    private static readonly Lazy<string> _dbPath = new(() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "manga-dl",
        "manga.db"));

    public static MangaDatabase Database { get; } = new(_dbPath.Value);

    public static HttpService Http { get; } = new()
    {
        BackendUrl = Settings.BackendUrl ?? string.Empty
    };

    public static ExtensionManager Extensions { get; } = new(
        Http,
        Path.Combine(AppContext.BaseDirectory, "Assets", "extensions"));

    public static DownloadManager Downloads { get; } = new(
        Database,
        Extensions,
        Http,
        Settings.DownloadPath);

    public static async Task InitializeAsync()
    {
        // 1. Initialize SQLite tables
        await Database.InitializeAsync();

        // 2. Restore saved accent
        var savedAccentName = Settings.AccentColor;
        var matchingAccent = ThemeService.Accents.FirstOrDefault(a =>
            a.Name.Equals(savedAccentName, StringComparison.OrdinalIgnoreCase)) ?? ThemeService.Accents[0];
        ThemeService.SetAccent(matchingAccent);

        // 3. Scan extensions
        Extensions.ScanExtensions();
    }
}
