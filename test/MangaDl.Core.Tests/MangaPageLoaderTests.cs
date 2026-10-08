using System.IO.Compression;
using MangaDl.Core.Local;
using Xunit;

namespace MangaDl.Core.Tests;

public class MangaPageLoaderTests
{
    [Fact]
    public void ExtractPagesFromCbz_ExtractsOnlyImagesInNaturalOrder()
    {
        var tempCbz = Path.Combine(Path.GetTempPath(), $"test_cbz_{Guid.NewGuid()}.cbz");
        try
        {
            using (var zip = ZipFile.Open(tempCbz, ZipArchiveMode.Create))
            {
                // Add entries in non-sorted order, including metadata
                var eXml = zip.CreateEntry("ComicInfo.xml");
                using (var s = eXml.Open()) s.Write([1, 2, 3]);

                var e10 = zip.CreateEntry("page_10.jpg");
                using (var s = e10.Open()) s.Write([10]);

                var e2 = zip.CreateEntry("page_2.png");
                using (var s = e2.Open()) s.Write([2]);

                var e1 = zip.CreateEntry("page_1.webp");
                using (var s = e1.Open()) s.Write([1]);

                var eTxt = zip.CreateEntry("notes.txt");
                using (var s = eTxt.Open()) s.Write([99]);
            }

            var pages = MangaPageLoader.ExtractPagesFromCbz(tempCbz);

            // Exactly 3 image pages
            Assert.Equal(3, pages.Count);

            // Natural order: page_1, page_2, page_10
            Assert.Equal(1, pages[0].PageNumber);
            Assert.Equal(new byte[] { 1 }, pages[0].ImageBytes);

            Assert.Equal(2, pages[1].PageNumber);
            Assert.Equal(new byte[] { 2 }, pages[1].ImageBytes);

            Assert.Equal(3, pages[2].PageNumber);
            Assert.Equal(new byte[] { 10 }, pages[2].ImageBytes);
        }
        finally
        {
            try { if (File.Exists(tempCbz)) File.Delete(tempCbz); } catch { }
        }
    }

    [Fact]
    public void FindLocalChapterCbz_FindsMatchingChapterFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"manga_dl_test_find_{Guid.NewGuid()}");
        var seriesDir = Path.Combine(tempDir, "Solo Leveling");
        Directory.CreateDirectory(seriesDir);

        try
        {
            var ch1 = Path.Combine(seriesDir, "Ch_1.cbz");
            var ch25 = Path.Combine(seriesDir, "Ch_2.5.cbz");
            File.WriteAllBytes(ch1, [1]);
            File.WriteAllBytes(ch25, [2]);

            var found1 = MangaPageLoader.FindLocalChapterCbz(tempDir, "Solo Leveling", 1.0);
            Assert.NotNull(found1);
            Assert.Equal(ch1, found1);

            var found25 = MangaPageLoader.FindLocalChapterCbz(tempDir, "Solo Leveling", 2.5);
            Assert.NotNull(found25);
            Assert.Equal(ch25, found25);

            var notFound = MangaPageLoader.FindLocalChapterCbz(tempDir, "Solo Leveling", 99.0);
            Assert.Null(notFound);
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
        }
    }
}
