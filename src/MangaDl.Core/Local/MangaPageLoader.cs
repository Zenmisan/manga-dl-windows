using System.IO.Compression;

namespace MangaDl.Core.Local;

/// <summary>
/// Helper to extract manga pages from CBZ archives or local directories,
/// and locate downloaded chapter files on disk.
/// </summary>
public static class MangaPageLoader
{
    /// <summary>
    /// Reads and returns all image pages from a local CBZ or ZIP file, ordered naturally.
    /// </summary>
    public static List<MangaPageInfo> ExtractPagesFromCbz(string cbzPath)
    {
        if (!File.Exists(cbzPath)) return [];

        var pages = new List<MangaPageInfo>();
        using var archive = ZipFile.OpenRead(cbzPath);

        var imageEntries = archive.Entries
            .Where(e => !string.IsNullOrEmpty(e.Name) && ArchiveInspector.ValidImageExtensions.Contains(Path.GetExtension(e.Name)))
            .OrderBy(e => e.FullName, ArchiveInspector.NaturalSortComparer.Instance)
            .ToList();

        var pageNum = 1;
        foreach (var entry in imageEntries)
        {
            using var stream = entry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            pages.Add(new MangaPageInfo(pageNum++, ImageBytes: ms.ToArray()));
        }

        return pages;
    }

    /// <summary>
    /// Reads image files from a local directory, ordered naturally.
    /// </summary>
    public static List<MangaPageInfo> ExtractPagesFromDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return [];

        var files = Directory.EnumerateFiles(directoryPath)
            .Where(f => ArchiveInspector.ValidImageExtensions.Contains(Path.GetExtension(f)))
            .OrderBy(f => Path.GetFileName(f), ArchiveInspector.NaturalSortComparer.Instance)
            .ToList();

        var pages = new List<MangaPageInfo>();
        var pageNum = 1;
        foreach (var file in files)
        {
            var bytes = File.ReadAllBytes(file);
            pages.Add(new MangaPageInfo(pageNum++, ImageBytes: bytes));
        }

        return pages;
    }

    /// <summary>
    /// Finds a local downloaded CBZ file for the given manga and chapter number/id if one exists.
    /// </summary>
    public static string? FindLocalChapterCbz(string downloadPath, string mangaTitle, double chapterNumber, string? chapterId = null)
    {
        if (string.IsNullOrWhiteSpace(downloadPath) || !Directory.Exists(downloadPath)) return null;

        var safeManga = FileNaming.SanitizeFileName(mangaTitle);
        var mangaFolder = Path.Combine(downloadPath, safeManga);
        if (!Directory.Exists(mangaFolder)) return null;

        var cbzFiles = Directory.EnumerateFiles(mangaFolder, "*.cbz").ToList();
        if (cbzFiles.Count == 0) return null;

        // 1. Try exact match on chapter number in filename
        foreach (var file in cbzFiles)
        {
            var fileName = Path.GetFileName(file);
            var num = ArchiveInspector.ExtractChapterNumber(fileName, -1);
            if (Math.Abs(num - chapterNumber) < 0.001)
            {
                return file;
            }
        }

        // 2. Try matching chapterId if provided
        if (!string.IsNullOrEmpty(chapterId))
        {
            foreach (var file in cbzFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (fileName.Contains(chapterId, StringComparison.OrdinalIgnoreCase))
                {
                    return file;
                }
            }
        }

        return null;
    }
}
