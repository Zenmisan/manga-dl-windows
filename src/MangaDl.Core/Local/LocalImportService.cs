using System.IO.Compression;
using MangaDl.Core.Database;
using MangaDl.Core.Database.Entities;
using MangaDl.Core.Downloads;

namespace MangaDl.Core.Local;

public enum LocalImportStatus { Done, Error, Unsupported }

public sealed record LocalImportResult(string Name, string Ext, LocalImportStatus Status, string Message);

/// <summary>
/// Imports local CBZ/ZIP files (and folders of loose images) into the library —
/// the "drop files to import" flow on ImportPage. Provider is fixed to "local"
/// so these rows are distinguishable from scraped-source entries everywhere else
/// (Library, Downloads, history all key off provider/mangaId already).
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
    /// Becomes its own one-chapter series.</summary>
    public async Task<LocalImportResult> ImportStandaloneFileAsync(string sourcePath)
    {
        var ext = Path.GetExtension(sourcePath);
        var name = Path.GetFileName(sourcePath);

        if (!ArchiveExtensions.Contains(ext))
        {
            return new LocalImportResult(name, ext.TrimStart('.').ToUpperInvariant(), LocalImportStatus.Unsupported,
                ext.Equals(".epub", StringComparison.OrdinalIgnoreCase)
                    ? "EPUB import isn't supported yet"
                    : "Unsupported format");
        }

        var title = Path.GetFileNameWithoutExtension(sourcePath);
        return await ImportArchiveAsChapterAsync(sourcePath, seriesTitle: title, chapterLabel: "Chapter 1", displayName: name);
    }

    /// <summary>A folder, dropped or picked. The folder name becomes the series.
    /// Each archive file directly inside it becomes a chapter; if there are no
    /// archive files but there are loose images, the whole folder becomes one chapter.</summary>
    public async Task<List<LocalImportResult>> ImportFolderAsync(string folderPath)
    {
        var results = new List<LocalImportResult>();
        var seriesTitle = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        var archiveFiles = Directory.EnumerateFiles(folderPath)
            .Where(f => ArchiveExtensions.Contains(Path.GetExtension(f)))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (archiveFiles.Count > 0)
        {
            foreach (var file in archiveFiles)
            {
                var chapterLabel = Path.GetFileNameWithoutExtension(file);
                results.Add(await ImportArchiveAsChapterAsync(file, seriesTitle, chapterLabel, Path.GetFileName(file)));
            }
            return results;
        }

        var imageFiles = Directory.EnumerateFiles(folderPath)
            .Where(f => ImageExtensions.Contains(Path.GetExtension(f)))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (imageFiles.Count > 0)
        {
            results.Add(await ImportImagesAsChapterAsync(imageFiles, seriesTitle, chapterLabel: seriesTitle, displayName: seriesTitle + "/"));
            return results;
        }

        results.Add(new LocalImportResult(seriesTitle + "/", "DIR", LocalImportStatus.Unsupported, "No CBZ/ZIP files or images found"));
        return results;
    }

    private async Task<LocalImportResult> ImportArchiveAsChapterAsync(string sourcePath, string seriesTitle, string chapterLabel, string displayName)
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

            var mangaId = FileNaming.Slugify(seriesTitle);
            var destDir = Path.Combine(_libraryRoot, FileNaming.SanitizeFileName(seriesTitle));
            Directory.CreateDirectory(destDir);
            var destPath = Path.Combine(destDir, FileNaming.SanitizeFileName(chapterLabel) + ".cbz");
            File.Copy(sourcePath, destPath, overwrite: true);

            await RegisterImportAsync(seriesTitle, mangaId, chapterLabel, destPath);
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

    private async Task<LocalImportResult> ImportImagesAsChapterAsync(List<string> imageFiles, string seriesTitle, string chapterLabel, string displayName)
    {
        try
        {
            var mangaId = FileNaming.Slugify(seriesTitle);
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

            await RegisterImportAsync(seriesTitle, mangaId, chapterLabel, destPath);
            return new LocalImportResult(displayName, "DIR", LocalImportStatus.Done, "Imported");
        }
        catch (Exception ex)
        {
            return new LocalImportResult(displayName, "DIR", LocalImportStatus.Error, ex.Message);
        }
    }

    private async Task RegisterImportAsync(string seriesTitle, string mangaId, string chapterLabel, string cbzPath)
    {
        await _db.AddToLibraryAsync(new LibraryEntity
        {
            Provider = Provider,
            MangaId = mangaId,
            Title = seriesTitle,
            Type = "manga"
        });

        await _db.EnqueueDownloadAsync(new DownloadEntity
        {
            Provider = Provider,
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
