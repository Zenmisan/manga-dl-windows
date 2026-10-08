using System.IO.Compression;
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

    [Fact]
    public async Task StandaloneFile_ValidCbz_ImportsAndRegistersInDatabase()
    {
        var cbzPath = Path.Combine(_sourceDir, "My Series Vol 1.cbz");
        WriteFakeCbz(cbzPath);

        var result = await _importer.ImportStandaloneFileAsync(cbzPath);

        Assert.Equal(LocalImportStatus.Done, result.Status);

        var library = await _db.GetLibraryAsync();
        var entry = Assert.Single(library);
        Assert.Equal(LocalImportService.Provider, entry.Provider);
        Assert.Equal("My Series Vol 1", entry.Title);

        var downloads = await _db.GetDownloadsAsync();
        var download = Assert.Single(downloads);
        Assert.Equal("completed", download.Status);
        Assert.True(File.Exists(download.CbzPath));
    }

    [Fact]
    public async Task StandaloneFile_UnsupportedExtension_ReturnsUnsupportedWithoutTouchingDatabase()
    {
        var pdfPath = Path.Combine(_sourceDir, "notes.pdf");
        await File.WriteAllTextAsync(pdfPath, "not a comic");

        var result = await _importer.ImportStandaloneFileAsync(pdfPath);

        Assert.Equal(LocalImportStatus.Unsupported, result.Status);
        Assert.Empty(await _db.GetLibraryAsync());
    }

    [Fact]
    public async Task StandaloneFile_CorruptArchive_ReturnsError()
    {
        var fakeZip = Path.Combine(_sourceDir, "broken.cbz");
        await File.WriteAllTextAsync(fakeZip, "this is not actually a zip file");

        var result = await _importer.ImportStandaloneFileAsync(fakeZip);

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
        Assert.Equal("Another Series", entry.Title);

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

        var cbzPath = Path.Combine(_sourceDir, "Extra Chapter.cbz");
        WriteFakeCbz(cbzPath);

        var result = await _importer.ImportStandaloneFileAsync(cbzPath, target);
        Assert.Equal(LocalImportStatus.Done, result.Status);

        // Still exactly one library entry — the existing one, not a new "local" series.
        var library = await _db.GetLibraryAsync();
        var entry = Assert.Single(library);
        Assert.Equal("mangadex", entry.Provider);
        Assert.Equal("solo-leveling", entry.MangaId);

        var downloads = await _db.GetDownloadsAsync();
        var download = Assert.Single(downloads);
        Assert.Equal("mangadex", download.Provider);
        Assert.Equal("solo-leveling", download.MangaId);
        Assert.Equal("Extra Chapter", download.ChapterTitle);
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
