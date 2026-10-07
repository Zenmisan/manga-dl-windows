namespace MangaDl.Core.Extensions;

public sealed record MangaSearchResult(
    string Id,
    string Title,
    string? CoverUrl,
    string Provider,
    string? Url = null,
    string? Status = null);

public sealed record ChapterItem(
    string Id,
    string Number,
    string Title,
    string Released,
    string? Scanlator = null,
    string? Url = null);

public sealed record MangaDetailResult(
    string Id,
    string Title,
    string? Description,
    string? CoverUrl,
    string? Author,
    string? Artist,
    string? Status,
    IReadOnlyList<string> Genres,
    IReadOnlyList<ChapterItem> Chapters);

public sealed record ChapterTextResult(
    string Content,
    string Format); // "html" | "plain"

public sealed record ExtensionMeta(
    string Id,
    string Name,
    string Language,
    string Version,
    string Type, // "manga" | "novel"
    string Initial,
    string Color,
    string ScriptPath);
