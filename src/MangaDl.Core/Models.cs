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
    string Source = "MangaDex")
{
    public string Id { get; set; } = Id;
    public string Title { get; set; } = Title;
    public string Cover { get; set; } = Cover;
    public int Unread { get; set; } = Unread;
    public bool Downloaded { get; set; } = Downloaded;
    public bool InLibrary { get; set; } = InLibrary;
    public string Source { get; set; } = Source;
}

public sealed record ContinueItem(Manga Manga, string Where, double Progress)
{
    public Manga Manga { get; set; } = Manga;
    public string Where { get; set; } = Where;
    public double Progress { get; set; } = Progress;
}

public sealed record Chapter(
    string Number,
    string Title,
    string Released,
    string Group,
    string Status,
    bool Read = false)
{
    public string Number { get; set; } = Number;
    public string Title { get; set; } = Title;
    public string Released { get; set; } = Released;
    public string Group { get; set; } = Group;
    public string Status { get; set; } = Status;
    public bool Read { get; set; } = Read;
}

public sealed record UpdateItem(
    Manga Manga,
    string Chapter,
    string Source,
    string? ChapterId = null,
    double ChapterNumber = 0,
    long DetectedAt = 0)
{
    public Manga Manga { get; set; } = Manga;
    public string Chapter { get; set; } = Chapter;
    public string Source { get; set; } = Source;
    public string? ChapterId { get; set; } = ChapterId;
    public double ChapterNumber { get; set; } = ChapterNumber;
    public long DetectedAt { get; set; } = DetectedAt;
}

public sealed record UpdateGroup(string Label, IReadOnlyList<UpdateItem> Items)
{
    public string Label { get; set; } = Label;
    public IReadOnlyList<UpdateItem> Items { get; set; } = Items;
}

public sealed record HistoryItem(Manga Manga, string Where, string When)
{
    public Manga Manga { get; set; } = Manga;
    public string Where { get; set; } = Where;
    public string When { get; set; } = When;
}

public enum NoticeKind { Info, Error, Success }

public sealed record Notice(
    string Title,
    string Body,
    string When,
    bool Unread,
    NoticeKind Kind,
    string Icon,
    string? Action = null)
{
    public string Title { get; set; } = Title;
    public string Body { get; set; } = Body;
    public string When { get; set; } = When;
    public bool Unread { get; set; } = Unread;
    public NoticeKind Kind { get; set; } = Kind;
    public string Icon { get; set; } = Icon;
    public string? Action { get; set; } = Action;
}

public enum DownloadState { Downloading, Queued, Paused, Failed, Done }

public sealed record DownloadItem(Manga Manga, string Chapter, string Status, double Progress, DownloadState State)
{
    public Manga Manga { get; set; } = Manga;
    public string Chapter { get; set; } = Chapter;
    public string Status { get; set; } = Status;
    public double Progress { get; set; } = Progress;
    public DownloadState State { get; set; } = State;
}

public sealed record Source(string Name, string Meta, string Initial, string Color)
{
    public string Name { get; set; } = Name;
    public string Meta { get; set; } = Meta;
    public string Initial { get; set; } = Initial;
    public string Color { get; set; } = Color;
}

public enum ExtensionState { UpdateAvailable, Installed, Available, Broken }

public sealed record Extension(string Name, string Initial, string Color, string Language, string Version, ExtensionState State)
{
    public string Name { get; set; } = Name;
    public string Initial { get; set; } = Initial;
    public string Color { get; set; } = Color;
    public string Language { get; set; } = Language;
    public string Version { get; set; } = Version;
    public ExtensionState State { get; set; } = State;
}

public sealed record Tracker(string Name, string Short, string Color, string Description, bool Connected = false, string? Username = null)
{
    public string Name { get; set; } = Name;
    public string Short { get; set; } = Short;
    public string Color { get; set; } = Color;
    public string Description { get; set; } = Description;
    public bool Connected { get; set; } = Connected;
    public string? Username { get; set; } = Username;
}

public sealed record ImportFile(string Name, string Ext, string Status, double Progress, string Kind)
{
    public string Name { get; set; } = Name;
    public string Ext { get; set; } = Ext;
    public string Status { get; set; } = Status;
    public double Progress { get; set; } = Progress;
    public string Kind { get; set; } = Kind;
}

public sealed record SearchGroup(string Source, string Meta, IReadOnlyList<Manga> Results)
{
    public string Source { get; set; } = Source;
    public string Meta { get; set; } = Meta;
    public IReadOnlyList<Manga> Results { get; set; } = Results;
}

public sealed record Bar(string Name, double Fraction)
{
    public string Name { get; set; } = Name;
    public double Fraction { get; set; } = Fraction;
}

public sealed record ReaderNavigationArgs(
    Manga Manga,
    string? ChapterId = null,
    string? ChapterTitle = null,
    double ChapterNumber = 1.0,
    int StartPage = 1)
{
    public Manga Manga { get; set; } = Manga;
    public string? ChapterId { get; set; } = ChapterId;
    public string? ChapterTitle { get; set; } = ChapterTitle;
    public double ChapterNumber { get; set; } = ChapterNumber;
    public int StartPage { get; set; } = StartPage;
}

public sealed record MangaPageInfo(
    int PageNumber,
    string? ImageUrl = null,
    byte[]? ImageBytes = null)
{
    public int PageNumber { get; set; } = PageNumber;
    public string? ImageUrl { get; set; } = ImageUrl;
    public byte[]? ImageBytes { get; set; } = ImageBytes;
}
