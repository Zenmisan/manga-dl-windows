using System.IO.Compression;
using System.Text;
using MangaDl.Core.Database;
using MangaDl.Core.Database.Entities;
using MangaDl.Core.Local;
using Xunit;

namespace MangaDl.Core.Tests;

public class LocalImportServiceTests : IAsyncLifetime
{
    private readonly string _tempDbPath = Path.Combine(Path.GetTempPath(), $"manga_import_test_{Guid.NewGuid()}.db");
    private readonly string _libraryRoot = Path.Combine(Path.GetTempPath(), $"manga_import_lib_{Guid.NewGuid()}");
    private readonly string _sourceDir = Path.Combine(Path.GetTempPath(), $"manga_import_src_{Guid.NewGuid()}");
    private MangaDatabase _db = null!;
    private LocalImportService _importer = null!;

    public async Task InitializeAsync()
    {
        _db = new MangaDatabase(_tempDbPath);
        await _db.InitializeAsync();
        _importer = new LocalImportService(_db, _libraryRoot);
        Directory.CreateDirectory(_sourceDir);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        foreach (var path in new[] { _tempDbPath })
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
        foreach (var dir in new[] { _libraryRoot, _sourceDir })
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    private static void WriteFakeCbz(string path, int imageCount = 2)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        for (var i = 0; i < imageCount; i++)
        {
            var entry = archive.CreateEntry($"{i:D3}.jpg");
            using var stream = entry.Open();
            stream.Write(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }); // fake JPEG header bytes, enough to exist as an entry
        }
    }

    private static void WriteMultiChapterCbz(string path, params (string Dir, int ImageCount)[] chapters)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (dir, count) in chapters)
        {
            for (var i = 0; i < count; i++)
            {
                var entry = archive.CreateEntry($"{dir}/{i:D3}.jpg");
                using var stream = entry.Open();
                stream.Write(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 });
            }
        }
    }

    [Fact]
    public async Task StandaloneFile_ValidCbz_ImportsAndRegistersInDatabase()
    {
        // No digits in the name at all, so ArchiveInspector finds nothing to parse out —
        // series title is the plain filename, chapter falls back to "Chapter 1",
        // matching web's archiveInspector.ts flat-archive behavior exactly.
        var cbzPath = Path.Combine(_sourceDir, "My Series.cbz");
        WriteFakeCbz(cbzPath);

        var results = await _importer.ImportStandaloneFileAsync(cbzPath);
        var result = Assert.Single(results);

        Assert.Equal(LocalImportStatus.Done, result.Status);

        var library = await _db.GetLibraryAsync();
        var entry = Assert.Single(library);
        Assert.Equal(LocalImportService.Provider, entry.Provider);
        Assert.Equal("My Series", entry.Title);

        var downloads = await _db.GetDownloadsAsync();
        var download = Assert.Single(downloads);
        Assert.Equal("Chapter 1", download.ChapterTitle);
        Assert.Equal("completed", download.Status);
        Assert.True(File.Exists(download.CbzPath));
    }

    [Fact]
    public async Task StandaloneFile_FilenameWithChapterNumber_ParsesSeriesAndChapterTitle()
    {
        // archiveInspector.ts's doc comment claims "One_Piece_Chapter_1080.cbz" -> series
        // "One Piece" — verified against real Node that's stale (see ArchiveInspectorTests
        // for why); the real behavior keeps "Chapter" attached to the series title since
        // the underscore separator isn't whitespace. Ported faithfully to match the
        // actual regex, not the comment.
        var cbzPath = Path.Combine(_sourceDir, "One_Piece_Chapter_1080.cbz");
        WriteFakeCbz(cbzPath);

        var results = await _importer.ImportStandaloneFileAsync(cbzPath);
        var result = Assert.Single(results);
        Assert.Equal(LocalImportStatus.Done, result.Status);

        var library = await _db.GetLibraryAsync();
        var entry = Assert.Single(library);
        Assert.Equal("One Piece Chapter", entry.Title);

        var download = Assert.Single(await _db.GetDownloadsAsync());
        Assert.Equal(1080, download.ChapterNumber);
        Assert.Equal("Chapter 1080", download.ChapterTitle);
    }

    [Fact]
    public async Task StandaloneFile_MultiChapterArchive_SplitsIntoSeparateChapters()
    {
        // One zip, three subfolders — archiveInspector.ts's "MangaKatana 10-chapter
        // subfolder format" pattern. Must become three chapters under one series,
        // not one chapter.
        var cbzPath = Path.Combine(_sourceDir, "Solo Leveling.cbz");
        WriteMultiChapterCbz(cbzPath, ("Chapter 1", 2), ("Chapter 2", 3), ("Chapter 10", 1));

        var results = await _importer.ImportStandaloneFileAsync(cbzPath);
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.Equal(LocalImportStatus.Done, r.Status));

        var library = await _db.GetLibraryAsync();
        var entry = Assert.Single(library);
        Assert.Equal("Solo Leveling", entry.Title);

        var downloads = (await _db.GetDownloadsAsync()).OrderBy(d => d.ChapterNumber).ToList();
        Assert.Equal(3, downloads.Count);
        // Natural sort must place "Chapter 2" before "Chapter 10" — ordinal sort would not.
        Assert.Equal(new[] { 1.0, 2.0, 10.0 }, downloads.Select(d => d.ChapterNumber));
        Assert.All(downloads, d => Assert.Equal(entry.MangaId, d.MangaId));
    }

    [Fact]
    public async Task StandaloneFile_UnsupportedExtension_ReturnsUnsupportedWithoutTouchingDatabase()
    {
        var pdfPath = Path.Combine(_sourceDir, "notes.pdf");
        await File.WriteAllTextAsync(pdfPath, "not a comic");

        var results = await _importer.ImportStandaloneFileAsync(pdfPath);
        var result = Assert.Single(results);

        Assert.Equal(LocalImportStatus.Unsupported, result.Status);
        Assert.Empty(await _db.GetLibraryAsync());
    }

    [Fact]
    public async Task StandaloneFile_CorruptArchive_ReturnsError()
    {
        var fakeZip = Path.Combine(_sourceDir, "broken.cbz");
        await File.WriteAllTextAsync(fakeZip, "this is not actually a zip file");

        var results = await _importer.ImportStandaloneFileAsync(fakeZip);
        var result = Assert.Single(results);

        Assert.Equal(LocalImportStatus.Error, result.Status);
        Assert.Empty(await _db.GetLibraryAsync());
    }

    [Fact]
    public async Task Folder_WithMultipleArchives_ImportsEachAsSeparateChapterUnderOneSeries()
    {
        var seriesDir = Path.Combine(_sourceDir, "Another Series");
        Directory.CreateDirectory(seriesDir);
        WriteFakeCbz(Path.Combine(seriesDir, "Chapter 1.cbz"));
        WriteFakeCbz(Path.Combine(seriesDir, "Chapter 2.cbz"));

        var results = await _importer.ImportFolderAsync(seriesDir);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(LocalImportStatus.Done, r.Status));

        var library = await _db.GetLibraryAsync();
        var entry = Assert.Single(library);
        Assert.Equal("Another Series", entry.Title); // folder name wins over each file's own parsed title

        var downloads = await _db.GetDownloadsAsync();
        Assert.Equal(2, downloads.Count);
        Assert.All(downloads, d => Assert.Equal(entry.MangaId, d.MangaId));
    }

    [Fact]
    public async Task Folder_WithLooseImagesOnly_BuildsOneCbzForTheWholeFolder()
    {
        var seriesDir = Path.Combine(_sourceDir, "Loose Images Series");
        Directory.CreateDirectory(seriesDir);
        await File.WriteAllBytesAsync(Path.Combine(seriesDir, "001.jpg"), new byte[] { 1, 2, 3 });
        await File.WriteAllBytesAsync(Path.Combine(seriesDir, "002.jpg"), new byte[] { 4, 5, 6 });

        var results = await _importer.ImportFolderAsync(seriesDir);

        var result = Assert.Single(results);
        Assert.Equal(LocalImportStatus.Done, result.Status);

        var downloads = await _db.GetDownloadsAsync();
        var download = Assert.Single(downloads);
        Assert.True(File.Exists(download.CbzPath));
        using var archive = ZipFile.OpenRead(download.CbzPath!);
        Assert.Contains(archive.Entries, e => e.Name.EndsWith(".xml"));
    }

    [Fact]
    public async Task StandaloneFile_WithTarget_AddsChapterUnderExistingSeriesInstead()
    {
        await _db.AddToLibraryAsync(new LibraryEntity
        {
            Provider = "mangadex",
            MangaId = "solo-leveling",
            Title = "Solo Leveling",
            Type = "manga"
        });
        var target = (await _db.GetLibraryAsync()).Single();

        var cbzPath = Path.Combine(_sourceDir, "Extra Chapter 5.cbz");
        WriteFakeCbz(cbzPath);

        var results = await _importer.ImportStandaloneFileAsync(cbzPath, target);
        var result = Assert.Single(results);
        Assert.Equal(LocalImportStatus.Done, result.Status);

        // Still exactly one library entry — the existing one, not a new "local" series.
        var library = await _db.GetLibraryAsync();
        var entry = Assert.Single(library);
        Assert.Equal("mangadex", entry.Provider);
        Assert.Equal("solo-leveling", entry.MangaId);

        var download = Assert.Single(await _db.GetDownloadsAsync());
        Assert.Equal("mangadex", download.Provider);
        Assert.Equal("solo-leveling", download.MangaId);
        Assert.Equal(5, download.ChapterNumber);
    }

    [Fact]
    public async Task Folder_WithNoSupportedContent_ReturnsUnsupported()
    {
        var emptyDir = Path.Combine(_sourceDir, "Empty Series");
        Directory.CreateDirectory(emptyDir);

        var results = await _importer.ImportFolderAsync(emptyDir);

        var result = Assert.Single(results);
        Assert.Equal(LocalImportStatus.Unsupported, result.Status);
    }
}
