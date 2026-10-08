namespace MangaDl.Core;

/// <summary>
/// Placeholder data so every page renders like the designs.
/// Replace with real repositories when wiring the backend.
/// Values in [brackets] are the same placeholders used in the mockups.
/// </summary>
public static class Sample
{
    public static readonly Manga HollowCrown = new("hollow-crown", "Hollow Crown", "#1A2433", Unread: 3, Downloaded: true, InLibrary: true);
    public static readonly Manga IronLantern = new("iron-lantern", "Iron Lantern", "#3A1518", Unread: 2, InLibrary: true, Source: "Asura Scans");
    public static readonly Manga AshenBlade = new("ashen-blade", "Ashen Blade", "#2E2412", Unread: 12, Downloaded: true, InLibrary: true);
    public static readonly Manga NinthArchive = new("ninth-archive", "The Ninth Archive", "#2D1716", Unread: 5, InLibrary: true, Source: "MangaKatana");
    public static readonly Manga PaperMoon = new("paper-moon", "Paper Moon Club", "#13282A", Unread: 1, InLibrary: true);
    public static readonly Manga RedThread = new("red-thread", "Red Thread Pact", "#311A1F", Unread: 2, Downloaded: true, InLibrary: true, Source: "Suwayomi");
    public static readonly Manga GlassGarden = new("glass-garden", "Glass Garden", "#1D2A1A", InLibrary: true);
    public static readonly Manga Tidebreaker = new("tidebreaker", "Tidebreaker", "#1B2030", Downloaded: true, InLibrary: true);
    public static readonly Manga NightMarket = new("night-market", "Night Market Ghosts", "#2B1A2E", InLibrary: true);
    public static readonly Manga SaltStatic = new("salt-static", "Salt and Static", "#22222A", InLibrary: true);

    public static IReadOnlyList<Manga> Library { get; } =
    [
        HollowCrown, NightMarket, AshenBlade, PaperMoon, Tidebreaker, NinthArchive, GlassGarden,
        RedThread, SaltStatic, IronLantern,
        new("seven-bells", "Seven Bells", "#1B2030"),
        new("cold-forge", "Cold Forge", "#311A1F", Unread: 4),
        new("second-sun", "Second Sun", "#13282A"),
        new("ivory-wolves", "Ivory Wolves", "#2D1716"),
    ];

    public static IReadOnlyList<ContinueItem> ContinueReading { get; } =
    [
        new(IronLantern, "Ch. 112 · page 14 of 38", 0.37),
        new(HollowCrown, "Ch. 48 · page 2 of 41", 0.05),
        new(RedThread, "Ch. 9 · page 30 of 32", 0.94),
    ];

    public static IReadOnlyList<string> Categories { get; } = ["All · 42", "Reading", "Plan to read", "Completed", "Local files"];

    public static IReadOnlyList<Chapter> Chapters { get; } =
    [
        new("51", "[Chapter title]", "[date]", "[scanlator]", "Download"),
        new("50", "[Chapter title]", "[date]", "[scanlator]", "Downloaded"),
        new("49", "[Chapter title]", "[date]", "[scanlator]", "Downloaded"),
        new("48", "[Chapter title]", "[date]", "[scanlator]", "Page 14"),
        new("47", "[Chapter title]", "[date]", "[scanlator]", "Read", Read: true),
        new("46", "[Chapter title]", "[date]", "[scanlator]", "Read", Read: true),
        new("45", "[Chapter title]", "[date]", "[scanlator]", "Read", Read: true),
        new("44", "[Chapter title]", "[date]", "[scanlator]", "Read", Read: true),
    ];

    public static IReadOnlyList<UpdateGroup> Updates { get; } =
    [
        new("Today",
        [
            new(HollowCrown, "Ch. 51 · [title]", "MangaDex"),
            new(AshenBlade, "Ch. 89 · [title]", "MangaDex"),
            new(AshenBlade, "Ch. 88 · [title]", "MangaDex"),
            new(IronLantern, "Ch. 113 · [title]", "Asura Scans"),
        ]),
        new("Yesterday",
        [
            new(NinthArchive, "Ch. 13 · [title]", "MangaKatana"),
            new(PaperMoon, "Ch. 4 · [title]", "MangaDex"),
            new(RedThread, "Ch. 10 · [title]", "Suwayomi"),
        ]),
    ];

    public static IReadOnlyList<HistoryItem> History { get; } =
    [
        new(IronLantern, "Ch. 112 · page 14 of 38", "Today · [time]"),
        new(HollowCrown, "Ch. 48 · page 2 of 41", "Today · [time]"),
        new(RedThread, "Ch. 9 · page 30 of 32", "Yesterday"),
        new(GlassGarden, "Ch. 21 · finished", "[weekday]"),
        new(Tidebreaker, "Ch. 7 · page 11 of 26", "[weekday]"),
        new(SaltStatic, "Ch. 2 · finished", "[weekday]"),
        new(AshenBlade, "Ch. 87 · finished", "[weekday]"),
        new(NightMarket, "Ch. 30 · page 5 of 22", "[weekday]"),
    ];

    public static IReadOnlyList<Notice> Notices { get; } =
    [
        new("3 new chapters", "Hollow Crown Ch. 51, Ashen Blade Ch. 88–89", "[time]", true, NoticeKind.Info, "Updates", "View"),
        new("Download failed", "Paper Moon Club Ch. 3 — source timed out", "[time]", true, NoticeKind.Error, "Warning", "Retry"),
        new("Downloads finished", "4 chapters saved to ~/manga-library", "[time]", false, NoticeKind.Success, "Download", "Open Folder"),
        new("Library synced", "Progress synced across your devices", "Yesterday", false, NoticeKind.Info, "Refresh"),
        new("Update available", "manga-dl [version] is ready to install", "Yesterday", false, NoticeKind.Info, "ArrowUp", "Install"),
        new("2 extensions updated", "Asura Scans and [Extension]", "[weekday]", false, NoticeKind.Info, "Refresh"),
    ];

    public static IReadOnlyList<DownloadItem> Downloads { get; } =
    [
        new(AshenBlade, "Ch. 88", "Downloading · 24 / 41 pages", 0.58, DownloadState.Downloading),
        new(AshenBlade, "Ch. 89", "Queued", 0, DownloadState.Queued),
        new(NinthArchive, "Ch. 12", "Paused", 0.30, DownloadState.Paused),
        new(PaperMoon, "Ch. 3", "Failed · source timed out", 0.12, DownloadState.Failed),
        new(HollowCrown, "Ch. 51", "Done", 1, DownloadState.Done),
    ];

    public static Source LastUsedSource { get; } = new("MangaDex", "English · Built-in", "M", "#2D1716");

    public static IReadOnlyList<Source> Sources { get; } =
    [
        new("MangaKatana", "English · Built-in", "K", "#1A2433"),
        new("Komga", "Self-hosted · Built-in", "K", "#13282A"),
        new("Suwayomi", "Self-hosted · Built-in", "S", "#2B1A2E"),
        new("Asura Scans", "English · Extension", "A", "#2E2412"),
        new("[Extension source]", "[Language] · Extension", "—", "#22222A"),
    ];

    public static IReadOnlyList<Extension> Extensions { get; } =
    [
        new("Asura Scans", "A", "#2E2412", "English", "v[x] → v[y]", ExtensionState.UpdateAvailable),
        new("[Extension]", "—", "#22222A", "English", "v[x] → v[y]", ExtensionState.UpdateAvailable),
        new("[Extension]", "—", "#2B1A2E", "English", "v[x]", ExtensionState.Installed),
        new("[Extension]", "—", "#13282A", "English", "v[x]", ExtensionState.Available),
        new("[Extension]", "—", "#1A2433", "Multi", "v[x]", ExtensionState.Available),
        new("[Extension]", "—", "#311A1F", "English", "v[x]", ExtensionState.Available),
        new("[Extension]", "—", "#1D2A1A", "English", "v[x]", ExtensionState.Broken),
    ];

    public static IReadOnlyList<Tracker> Trackers { get; } =
    [
        new("AniList", "AL", "#1E3A5F", "Sync status, score, chapters read and dates."),
        new("MyAnimeList", "MAL", "#1F2C4F", "Sync manga status with MyAnimeList."),
        new("Kitsu", "K", "#3B1F2E", "Sync manga progress with Kitsu."),
        new("MangaUpdates", "MU", "#2E2412", "Keep your MangaUpdates lists current."),
        new("Shikimori", "S", "#22222A", "Sync progress with Shikimori."),
        new("Bangumi", "B", "#311A1F", "Sync progress with Bangumi."),
    ];

    public static IReadOnlyList<SearchGroup> SearchResults { get; } =
    [
        new("MangaDex", "12 results",
        [
            HollowCrown,
            new("crown-of-ash", "Crown of Ash", "#2E2412"),
            new("paper-crown", "The Paper Crown", "#2B1A2E"),
            new("crownless", "Crownless", "#13282A"),
            new("crown-tide", "Crown & Tide", "#1B2030"),
            new("ember-crown", "Ember Crown", "#311A1F"),
            new("glass-crown", "Glass Crown", "#1D2A1A"),
        ]),
        new("MangaKatana", "4 results",
        [
            HollowCrown,
            new("iron-crown", "Iron Crown Saga", "#311A1F"),
            new("crown-tide-2", "Crown & Tide", "#1B2030"),
            new("thorn-crown", "Thorn Crown", "#22222A"),
        ]),
    ];

    public static IReadOnlyList<Manga> BrowseCatalog { get; } =
    [
        AshenBlade,
        new("lantern-street", "Lantern Street", "#1A2433"),
        new("ghost-ferry", "Ghost Ferry", "#2B1A2E"),
        new("second-sun-2", "Second Sun", "#13282A"),
        HollowCrown,
        new("ivory-wolves-2", "Ivory Wolves", "#2D1716"),
        new("low-tide", "Low Tide Saints", "#1D2A1A"),
        PaperMoon,
        new("neon-shrine", "Neon Shrine", "#22222A"),
        new("cold-forge-2", "Cold Forge", "#311A1F"),
        new("seven-bells-2", "Seven Bells", "#1B2030"),
        new("rust-garden", "Rust Garden", "#2E2412"),
        new("moth-kingdom", "Moth Kingdom", "#2B1A2E"),
        new("quiet-engine", "Quiet Engine", "#22222A"),
        new("blue-hour", "Blue Hour", "#1B2030"),
        new("saltwind", "Saltwind", "#13282A"),
    ];

    public static IReadOnlyList<ImportFile> ImportQueue { get; } =
    [
        new("[Series] Vol. 01.cbz", "CBZ", "Imported", 1, "done"),
        new("[Series] Vol. 02.cbz", "CBZ", "Importing…", 0.46, "busy"),
        new("[Series] extras/", "DIR", "Waiting", 0, "queued"),
        new("notes.pdf", "PDF", "Unsupported format", 1, "error"),
    ];

    public static IReadOnlyList<Bar> ByCategory { get; } =
        [new("Reading", 0.70), new("Completed", 0.45), new("Plan to read", 0.20), new("Local files", 0.12)];

    public static IReadOnlyList<Bar> BySource { get; } =
        [new("MangaDex", 0.64), new("MangaKatana", 0.24), new("Asura Scans", 0.18), new("Local files", 0.12)];

    /// <summary>Deterministic 0–3 intensity per day for the 52-week heatmap.</summary>
    public static IReadOnlyList<int> Activity { get; } =
        Enumerable.Range(0, 364).Select(i =>
        {
            var v = (i * 37 + i / 7 * 11) % 13;
            return v < 5 ? 0 : v < 9 ? 1 : v < 12 ? 2 : 3;
        }).ToList();
}
