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
/// the "drop files to import" flow on ImportPage. By default each import becomes
/// its own new series under provider "local". Pass an existing <see cref="LibraryEntity"/>
/// as the target to instead add the imported file(s) as chapters under that series
/// (which may be under any provider, not just "local") — the "Add to existing series"
/// destination option.
/// </summary>
public sealed class LocalImportService
{
    public const string Provider = "local";

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".avif", ".bmp"
    };

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

    /// <summary>A standalone archive file, dropped or picked directly (not inside a folder).
    /// Becomes its own one-chapter series, unless <paramref name="target"/> is given.</summary>
    public async Task<LocalImportResult> ImportStandaloneFileAsync(string sourcePath, LibraryEntity? target = null)
    {
        var ext = Path.GetExtension(sourcePath);
        var name = Path.GetFileName(sourcePath);

        if (ext.Equals(".epub", StringComparison.OrdinalIgnoreCase))
        {
            return await ImportEpubAsync(sourcePath, target);
        }

        if (!ArchiveExtensions.Contains(ext))
        {
            return new LocalImportResult(name, ext.TrimStart('.').ToUpperInvariant(), LocalImportStatus.Unsupported, "Unsupported format");
        }

        var chapterLabel = Path.GetFileNameWithoutExtension(sourcePath);
        var seriesTitle = target?.Title ?? chapterLabel;
        if (target is null) chapterLabel = "Chapter 1";

        return await ImportArchiveAsChapterAsync(sourcePath, seriesTitle, chapterLabel, name, target);
    }

    /// <summary>A folder, dropped or picked. The folder name becomes the series (unless
    /// <paramref name="target"/> is given). Each archive file directly inside it becomes
    /// a chapter; if there are no archive files but there are loose images, the whole
    /// folder becomes one chapter.</summary>
    public async Task<List<LocalImportResult>> ImportFolderAsync(string folderPath, LibraryEntity? target = null)
    {
        var results = new List<LocalImportResult>();
        var folderName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var seriesTitle = target?.Title ?? folderName;

        var archiveFiles = Directory.EnumerateFiles(folderPath)
            .Where(f => ArchiveExtensions.Contains(Path.GetExtension(f)))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (archiveFiles.Count > 0)
        {
            foreach (var file in archiveFiles)
            {
                var chapterLabel = Path.GetFileNameWithoutExtension(file);
                results.Add(await ImportArchiveAsChapterAsync(file, seriesTitle, chapterLabel, Path.GetFileName(file), target));
            }
            return results;
        }

        var imageFiles = Directory.EnumerateFiles(folderPath)
            .Where(f => ImageExtensions.Contains(Path.GetExtension(f)))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (imageFiles.Count > 0)
        {
            results.Add(await ImportImagesAsChapterAsync(imageFiles, seriesTitle, chapterLabel: folderName, displayName: folderName + "/", target));
            return results;
        }

        results.Add(new LocalImportResult(folderName + "/", "DIR", LocalImportStatus.Unsupported, "No CBZ/ZIP files or images found"));
        return results;
    }

    private async Task<LocalImportResult> ImportArchiveAsChapterAsync(string sourcePath, string seriesTitle, string chapterLabel, string displayName, LibraryEntity? target)
    {
        try
        {
            using (var archive = ZipFile.OpenRead(sourcePath))
            {
                var hasImage = archive.Entries.Any(e => ImageExtensions.Contains(Path.GetExtension(e.Name)));
                if (!hasImage)
                {
                    return new LocalImportResult(displayName, "CBZ", LocalImportStatus.Error, "No images found inside");
                }
            }

            var destDir = Path.Combine(_libraryRoot, FileNaming.SanitizeFileName(seriesTitle));
            Directory.CreateDirectory(destDir);
            var destPath = Path.Combine(destDir, FileNaming.SanitizeFileName(chapterLabel) + ".cbz");
            File.Copy(sourcePath, destPath, overwrite: true);

            await RegisterImportAsync(seriesTitle, chapterLabel, destPath, target);
            return new LocalImportResult(displayName, "CBZ", LocalImportStatus.Done, "Imported");
        }
        catch (InvalidDataException)
        {
            return new LocalImportResult(displayName, "CBZ", LocalImportStatus.Error, "Not a valid ZIP/CBZ archive");
        }
        catch (Exception ex)
        {
            return new LocalImportResult(displayName, "CBZ", LocalImportStatus.Error, ex.Message);
        }
    }

    private async Task<LocalImportResult> ImportImagesAsChapterAsync(List<string> imageFiles, string seriesTitle, string chapterLabel, string displayName, LibraryEntity? target)
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

            await CbzBuilder.BuildCbzAsync(destPath, seriesTitle, chapterLabel, "1", pageImages, extensions);

            await RegisterImportAsync(seriesTitle, chapterLabel, destPath, target);
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

    private async Task RegisterImportAsync(string seriesTitle, string chapterLabel, string cbzPath, LibraryEntity? target)
    {
        var provider = target?.Provider ?? Provider;
        var mangaId = target?.MangaId ?? FileNaming.Slugify(seriesTitle);

        if (target is null)
        {
            await _db.AddToLibraryAsync(new LibraryEntity
            {
                Provider = provider,
                MangaId = mangaId,
                Title = seriesTitle,
                Type = "manga"
            });
        }

        await _db.EnqueueDownloadAsync(new DownloadEntity
        {
            Provider = provider,
            MangaId = mangaId,
            MangaTitle = seriesTitle,
            ChapterId = FileNaming.Slugify(chapterLabel),
            ChapterTitle = chapterLabel,
            CbzPath = cbzPath,
            Status = "completed",
            Progress = 100,
            CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
    }
}
