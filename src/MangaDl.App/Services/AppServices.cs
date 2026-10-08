using MangaDl.Core.Auth;
using MangaDl.Core.Backup;
using MangaDl.Core.Database;
using MangaDl.Core.Downloads;
using MangaDl.Core.Extensions;
using MangaDl.Core.Http;
using MangaDl.Core.Local;
using MangaDl.Core.Sync;
using MangaDl.Core.Tracking;

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

    public static SupabaseAuthService Auth { get; } = new(
        Http,
        Settings.SupabaseUrl ?? AppSettings.DefaultSupabaseUrl,
        Settings.SupabaseAnonKey ?? AppSettings.DefaultSupabaseAnonKey);

    public static LocalImportService LocalImport { get; } = new(
        Database,
        Settings.DownloadPath);

    public static TachibkImportService TachibkImport { get; } = new(Database);
    public static MangadlBackupService Backup { get; } = new(Database);

    public static SupabaseSyncService Sync { get; } = new(
        Http,
        Database,
        Settings.SupabaseUrl ?? AppSettings.DefaultSupabaseUrl,
        Settings.SupabaseAnonKey ?? AppSettings.DefaultSupabaseAnonKey,
        credentialProvider: () =>
        {
            var (accessToken, _) = CredentialStore.Load();
            return (Settings.UserId, accessToken);
        });

    public static TrackerService Trackers { get; } = new(Http);
    public static OAuthLoopbackListener Loopback { get; } = new(5678);

    public static async Task SyncTrackerProgressAsync(
        string provider,
        string mangaId,
        string mangaTitle,
        double chapterNumber,
        bool isCompleted)
    {
        if (!Settings.AutoSyncTrackers) return;
        if (!Settings.AnilistConnected && !Settings.MalConnected) return;

        try
        {
            await Trackers.SyncChapterProgressAsync(
                Database,
                provider,
                mangaId,
                mangaTitle,
                chapterNumber,
                isCompleted,
                trackerKey => CredentialStore.LoadTrackerToken(trackerKey),
                Settings.AutoSyncTrackers,
                Settings.MarkTrackerReadingOnFirstChapter);
        }
        catch (Exception ex)
        {
            AppLog.Warn("AppServices.SyncTrackerProgressAsync", ex);
        }
    }

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

        // 4. Background cloud sync on app start if signed in
        if (!string.IsNullOrEmpty(Settings.UserId))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await Sync.SyncAllAsync();
                }
                catch (Exception ex)
                {
                    AppLog.Warn("AppServices.InitialSync", ex);
                }
            });
        }
    }
}
