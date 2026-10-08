using System.IO.Compression;
using System.Text;
using MangaDl.Core.Database;
using MangaDl.Core.Database.Entities;
using MangaDl.Core.Downloads;

namespace MangaDl.Core.Local;

public enum LocalImportStatus { Done, Error, Unsupported }

public sealed record LocalImportResult(string Name, string Ext, LocalImportStatus Status, string Message);

/// <summary>
/// Imports local CBZ/ZIP files (and folders of loose images) into the library —
/// the "drop files to import" flow on ImportPage. Mirrors the web app's
/// lib/archiveInspector.ts behavior exactly: filename-based chapter/range
/// detection, natural sort, and the same three directory-structure patterns
/// (a single archive can legitimately bundle several chapters as subfolders —
/// e.g. "Solo_Leveling_01-10.zip" containing "Chapter 1/", "Chapter 2/", ...).
/// By default each import becomes its own new series under provider "local".
/// Pass an existing <see cref="LibraryEntity"/> as the target to instead add the
/// imported chapter(s) under that series (which may be under any provider) —
/// the "Add to existing series" destination option.
/// </summary>
public sealed class LocalImportService
{
    public const string Provider = "local";

    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cbz", ".zip"
    };

    private readonly MangaDatabase _db;
    private readonly string _libraryRoot;

    public LocalImportService(MangaDatabase db, string libraryRoot)
    {
        _db = db;
        _libraryRoot = libraryRoot;
    }

    /// <summary>A standalone file, dropped or picked directly (not inside a folder).
    /// A CBZ/ZIP may expand into several chapters if it bundles subfolders per
    /// chapter — this always returns a list for that reason, even for the common
    /// one-chapter case.</summary>
    public async Task<List<LocalImportResult>> ImportStandaloneFileAsync(string sourcePath, LibraryEntity? target = null)
    {
        var ext = Path.GetExtension(sourcePath);
        var name = Path.GetFileName(sourcePath);

        if (ext.Equals(".epub", StringComparison.OrdinalIgnoreCase))
        {
            return [await ImportEpubAsync(sourcePath, target)];
        }

        if (!ArchiveExtensions.Contains(ext))
        {
            return [new LocalImportResult(name, ext.TrimStart('.').ToUpperInvariant(), LocalImportStatus.Unsupported, "Unsupported format")];
        }

        return await ImportArchiveFileAsync(sourcePath, target);
    }

    /// <summary>A folder, dropped or picked. The folder name becomes the series (unless
    /// <paramref name="target"/> is given) — overriding whatever each archive's own
    /// filename would otherwise suggest, since the folder is the stronger signal here.
    /// Each archive file directly inside it is expanded via the same per-archive logic
    /// as <see cref="ImportStandaloneFileAsync"/>; if there are no archive files but
    /// there are loose images, the whole folder becomes one chapter.</summary>
    public async Task<List<LocalImportResult>> ImportFolderAsync(string folderPath, LibraryEntity? target = null)
    {
        var results = new List<LocalImportResult>();
        var folderName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var seriesTitle = target?.Title ?? folderName;

        var archiveFiles = Directory.EnumerateFiles(folderPath)
            .Where(f => ArchiveExtensions.Contains(Path.GetExtension(f)))
            .OrderBy(f => f, ArchiveInspector.NaturalSortComparer.Instance)
            .ToList();

        if (archiveFiles.Count > 0)
        {
            foreach (var file in archiveFiles)
            {
                results.AddRange(await ImportArchiveFileAsync(file, target, seriesTitleOverride: seriesTitle));
            }
            return results;
        }

        var imageFiles = Directory.EnumerateFiles(folderPath)
            .Where(f => ArchiveInspector.ValidImageExtensions.Contains(Path.GetExtension(f)))
            .OrderBy(f => f, ArchiveInspector.NaturalSortComparer.Instance)
            .ToList();

        if (imageFiles.Count > 0)
        {
            var chNum = ArchiveInspector.ExtractChapterNumber(folderName, 1);
            results.Add(await ImportImagesAsChapterAsync(imageFiles, seriesTitle, chapterLabel: folderName, chapterNumber: chNum, displayName: folderName + "/", target));
            return results;
        }

        results.Add(new LocalImportResult(folderName + "/", "DIR", LocalImportStatus.Unsupported, "No CBZ/ZIP files or images found"));
        return results;
    }

    /// <summary>Opens the archive, detects its directory structure (multi-chapter
    /// subfolders / single subfolder / flat), and builds one new CBZ per detected
    /// chapter — always re-extracting and rebuilding (rather than copying the
    /// original file) so natural sort order is actually applied to page order,
    /// not just trusted from the zip's own entry order.</summary>
    private async Task<List<LocalImportResult>> ImportArchiveFileAsync(string sourcePath, LibraryEntity? target, string? seriesTitleOverride = null)
    {
        var displayName = Path.GetFileName(sourcePath);
        try
        {
            using var archive = ZipFile.OpenRead(sourcePath);
            var allImagePaths = archive.Entries
                .Where(e => e.Name.Length > 0 && ArchiveInspector.ValidImageExtensions.Contains(Path.GetExtension(e.Name)))
                .Select(e => e.FullName)
                .ToList();

            if (allImagePaths.Count == 0)
            {
                return [new LocalImportResult(displayName, "CBZ", LocalImportStatus.Error, "No images found inside")];
            }

            var meta = ArchiveInspector.ParseArchiveFilename(displayName);
            var seriesTitle = target?.Title ?? seriesTitleOverride ?? meta.SeriesTitle;

            var directoryMap = new Dictionary<string, List<string>>();
            var rootFiles = new List<string>();
            foreach (var path in allImagePaths)
            {
                var parts = path.Split('/').Where(p => p.Trim().Length > 0).ToArray();
                if (parts.Length > 1)
                {
                    if (!directoryMap.TryGetValue(parts[0], out var list)) { list = []; directoryMap[parts[0]] = list; }
                    list.Add(path);
                }
                else
                {
                    rootFiles.Add(path);
                }
            }

            var chapterGroups = new List<(double Number, string Title, List<string> Paths)>();

            if (directoryMap.Count > 1)
            {
                // Pattern A: multi-chapter archive, one subfolder per chapter.
                var sortedDirs = directoryMap.Keys.OrderBy(d => d, ArchiveInspector.NaturalSortComparer.Instance).ToList();
                var idx = 1;
                foreach (var dir in sortedDirs)
                {
                    var paths = directoryMap[dir];
                    paths.Sort(ArchiveInspector.NaturalSortComparer.Instance);
                    var fallback = meta.RangeStart.HasValue ? meta.RangeStart.Value + (idx - 1) : idx;
                    var chNum = ArchiveInspector.ExtractChapterNumber(dir, fallback);
                    chapterGroups.Add((chNum, ArchiveInspector.CleanDirTitle(dir, chNum), paths));
                    idx++;
                }
            }
            else if (directoryMap.Count == 1 && rootFiles.Count == 0)
            {
                // Single subfolder holding everything — still one chapter.
                var dir = directoryMap.Keys.First();
                var paths = directoryMap[dir];
                paths.Sort(ArchiveInspector.NaturalSortComparer.Instance);
                var chNum = ArchiveInspector.ExtractChapterNumber(dir, meta.RangeStart ?? 1);
                chapterGroups.Add((chNum, ArchiveInspector.CleanDirTitle(dir, chNum), paths));
            }
            else
            {
                // Pattern B: flat archive — standard single chapter.
                var all = new List<string>(rootFiles);
                all.AddRange(directoryMap.Values.SelectMany(v => v));
                all.Sort(ArchiveInspector.NaturalSortComparer.Instance);
                var chNum = meta.RangeStart ?? 1;
                var title = meta.IsRange
                    ? $"{seriesTitle} ({ArchiveInspector.FormatChapterNumber(meta.RangeStart ?? 0)}-{ArchiveInspector.FormatChapterNumber(meta.RangeEnd ?? 0)})"
                    : $"Chapter {ArchiveInspector.FormatChapterNumber(chNum)}";
                chapterGroups.Add((chNum, title, all));
            }

            chapterGroups.Sort((a, b) => a.Number.CompareTo(b.Number));

            var provider = target?.Provider ?? Provider;
            var mangaId = target?.MangaId ?? FileNaming.Slugify(seriesTitle);
            var destDir = Path.Combine(_libraryRoot, FileNaming.SanitizeFileName(seriesTitle));
            Directory.CreateDirectory(destDir);

            if (target is null)
            {
                await _db.AddToLibraryAsync(new LibraryEntity { Provider = provider, MangaId = mangaId, Title = seriesTitle, Type = "manga" });
            }

            var results = new List<LocalImportResult>();
            foreach (var (chNum, chTitle, paths) in chapterGroups)
            {
                var pageImages = new List<byte[]>(paths.Count);
                var extensions = new List<string>(paths.Count);
                foreach (var path in paths)
                {
                    var entry = archive.GetEntry(path)!;
                    using var buffer = new MemoryStream();
                    using (var entryStream = entry.Open()) await entryStream.CopyToAsync(buffer);
                    pageImages.Add(buffer.ToArray());
                    extensions.Add(Path.GetExtension(path));
                }

                var destPath = Path.Combine(destDir, FileNaming.SanitizeFileName(chTitle) + ".cbz");
                await CbzBuilder.BuildCbzAsync(destPath, seriesTitle, chTitle, ArchiveInspector.FormatChapterNumber(chNum), pageImages, extensions);

                await _db.EnqueueDownloadAsync(new DownloadEntity
                {
                    Provider = provider,
                    MangaId = mangaId,
                    MangaTitle = seriesTitle,
                    ChapterId = FileNaming.Slugify(chTitle),
                    ChapterTitle = chTitle,
                    ChapterNumber = chNum,
                    CbzPath = destPath,
                    Status = "completed",
                    Progress = 100,
                    CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });

                results.Add(new LocalImportResult(
                    chapterGroups.Count > 1 ? $"{displayName} — {chTitle}" : displayName,
                    "CBZ", LocalImportStatus.Done, "Imported"));
            }

            return results;
        }
        catch (InvalidDataException)
        {
            return [new LocalImportResult(displayName, "CBZ", LocalImportStatus.Error, "Not a valid ZIP/CBZ archive")];
        }
        catch (Exception ex)
        {
            return [new LocalImportResult(displayName, "CBZ", LocalImportStatus.Error, ex.Message)];
        }
    }

    private async Task<LocalImportResult> ImportImagesAsChapterAsync(List<string> imageFiles, string seriesTitle, string chapterLabel, double chapterNumber, string displayName, LibraryEntity? target)
    {
        try
        {
            var destDir = Path.Combine(_libraryRoot, FileNaming.SanitizeFileName(seriesTitle));
            Directory.CreateDirectory(destDir);
            var destPath = Path.Combine(destDir, FileNaming.SanitizeFileName(chapterLabel) + ".cbz");

            var pageImages = new List<byte[]>(imageFiles.Count);
            var extensions = new List<string>(imageFiles.Count);
            foreach (var file in imageFiles)
            {
                pageImages.Add(await File.ReadAllBytesAsync(file));
                extensions.Add(Path.GetExtension(file));
            }

            await CbzBuilder.BuildCbzAsync(destPath, seriesTitle, chapterLabel, ArchiveInspector.FormatChapterNumber(chapterNumber), pageImages, extensions);

            var provider = target?.Provider ?? Provider;
            var mangaId = target?.MangaId ?? FileNaming.Slugify(seriesTitle);

            if (target is null)
            {
                await _db.AddToLibraryAsync(new LibraryEntity { Provider = provider, MangaId = mangaId, Title = seriesTitle, Type = "manga" });
            }

            await _db.EnqueueDownloadAsync(new DownloadEntity
            {
                Provider = provider,
                MangaId = mangaId,
                MangaTitle = seriesTitle,
                ChapterId = FileNaming.Slugify(chapterLabel),
                ChapterTitle = chapterLabel,
                ChapterNumber = chapterNumber,
                CbzPath = destPath,
                Status = "completed",
                Progress = 100,
                CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });

            return new LocalImportResult(displayName, "DIR", LocalImportStatus.Done, "Imported");
        }
        catch (Exception ex)
        {
            return new LocalImportResult(displayName, "DIR", LocalImportStatus.Error, ex.Message);
        }
    }

    /// <summary>EPUB chapters have no page images to zip into a CBZ — each chapter's
    /// XHTML is written to its own file under the series folder instead, and the
    /// existing DownloadEntity.CbzPath column (despite the name) just points at that
    /// text file. NovelReaderPage reads it back the same way it reads any other
    /// extension-provided chapter, just from disk instead of over the network.</summary>
    private async Task<LocalImportResult> ImportEpubAsync(string sourcePath, LibraryEntity? target)
    {
        var displayName = Path.GetFileName(sourcePath);
        try
        {
            var bytes = await File.ReadAllBytesAsync(sourcePath);
            var book = EpubParser.Parse(bytes);
            if (book.Chapters.Count == 0)
            {
                return new LocalImportResult(displayName, "EPUB", LocalImportStatus.Error, "No chapters found inside");
            }

            var provider = target?.Provider ?? Provider;
            var seriesTitle = target?.Title ?? (string.IsNullOrWhiteSpace(book.Title) ? Path.GetFileNameWithoutExtension(sourcePath) : book.Title);
            var mangaId = target?.MangaId ?? FileNaming.Slugify(seriesTitle);

            var destDir = Path.Combine(_libraryRoot, FileNaming.SanitizeFileName(seriesTitle), "chapters");
            Directory.CreateDirectory(destDir);

            if (target is null)
            {
                await _db.AddToLibraryAsync(new LibraryEntity
                {
                    Provider = provider,
                    MangaId = mangaId,
                    Title = seriesTitle,
                    Type = "novel"
                });
            }

            for (var i = 0; i < book.Chapters.Count; i++)
            {
                var chapter = book.Chapters[i];
                var chapterId = $"{i + 1:D4}";
                var chapterPath = Path.Combine(destDir, $"{chapterId}.html");
                await File.WriteAllTextAsync(chapterPath, chapter.Html, Encoding.UTF8);

                await _db.EnqueueDownloadAsync(new DownloadEntity
                {
                    Provider = provider,
                    MangaId = mangaId,
                    MangaTitle = seriesTitle,
                    ChapterId = chapterId,
                    ChapterTitle = chapter.Title,
                    ChapterNumber = i + 1,
                    CbzPath = chapterPath,
                    Status = "completed",
                    Progress = 100,
                    CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });
            }

            return new LocalImportResult(displayName, "EPUB", LocalImportStatus.Done, $"Imported {book.Chapters.Count} chapters");
        }
        catch (Exception ex)
        {
            return new LocalImportResult(displayName, "EPUB", LocalImportStatus.Error, ex.Message);
        }
    }
}
