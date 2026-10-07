namespace MangaDl.Core;

// Plain models shared by the UI. No Windows types here, so this project can
// also back the future Linux app. Colours are hex strings ("#1A2433") that the
// UI turns into brushes; replace Cover with an image URL when wiring sources.

public sealed record Manga(
    string Id,
    string Title,
    string Cover,
    int Unread = 0,
    bool Downloaded = false,
    bool InLibrary = false,
    string Source = "MangaDex");

public sealed record ContinueItem(Manga Manga, string Where, double Progress);

public sealed record Chapter(
    string Number,
    string Title,
    string Released,
    string Group,
    string Status,
    bool Read = false);

public sealed record UpdateItem(Manga Manga, string Chapter, string Source);

public sealed record UpdateGroup(string Label, IReadOnlyList<UpdateItem> Items);

public sealed record HistoryItem(Manga Manga, string Where, string When);

public enum NoticeKind { Info, Error, Success }

public sealed record Notice(
    string Title,
    string Body,
    string When,
    bool Unread,
    NoticeKind Kind,
    string Icon,
    string? Action = null);

public enum DownloadState { Downloading, Queued, Paused, Failed, Done }

public sealed record DownloadItem(Manga Manga, string Chapter, string Status, double Progress, DownloadState State);

public sealed record Source(string Name, string Meta, string Initial, string Color);

public enum ExtensionState { UpdateAvailable, Installed, Available, Broken }

public sealed record Extension(string Name, string Initial, string Color, string Language, string Version, ExtensionState State);

public sealed record Tracker(string Name, string Short, string Color, string Description, bool Connected = false);

public sealed record ImportFile(string Name, string Ext, string Status, double Progress, string Kind);

public sealed record SearchGroup(string Source, string Meta, IReadOnlyList<Manga> Results);

public sealed record Bar(string Name, double Fraction);
