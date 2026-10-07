using System.IO.Compression;
using System.Security;
using System.Text;

namespace MangaDl.Core.Downloads;

public static class CbzBuilder
{
    public static async Task BuildCbzAsync(
        string targetFilePath,
        string mangaTitle,
        string chapterTitle,
        string? chapterNumber,
        IReadOnlyList<byte[]> pageImages,
        IReadOnlyList<string>? fileExtensions = null,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(targetFilePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // Write to temporary file first to avoid corrupted CBZ on partial write
        var tempFile = targetFilePath + ".tmp";
        if (File.Exists(tempFile)) File.Delete(tempFile);

        try
        {
            using (var zipStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                var count = pageImages.Count;
                for (var i = 0; i < count; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    var ext = fileExtensions != null && i < fileExtensions.Count
                        ? fileExtensions[i]
                        : ".jpg";

                    if (!ext.StartsWith('.')) ext = "." + ext;

                    var entryName = $"{i + 1:D3}{ext}";
                    var entry = zip.CreateEntry(entryName, CompressionLevel.NoCompression);

                    using (var entryStream = entry.Open())
                    {
                        await entryStream.WriteAsync(pageImages[i], ct);
                    }

                    progress?.Report((double)(i + 1) / (count + 1));
                }

                // Add ComicInfo.xml for reader compatibility (Kavita, Komga, Tachiyomi, Paperback)
                var comicInfoXml = GenerateComicInfoXml(mangaTitle, chapterTitle, chapterNumber, count);
                var ciEntry = zip.CreateEntry("ComicInfo.xml", CompressionLevel.Fastest);
                using (var ciStream = ciEntry.Open())
                {
                    var xmlBytes = Encoding.UTF8.GetBytes(comicInfoXml);
                    await ciStream.WriteAsync(xmlBytes, ct);
                }

                progress?.Report(1.0);
            }

            if (File.Exists(targetFilePath)) File.Delete(targetFilePath);
            File.Move(tempFile, targetFilePath);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }

    private static string GenerateComicInfoXml(string series, string title, string? number, int pageCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<ComicInfo xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">");
        sb.AppendLine($"  <Series>{SecurityElement.Escape(series)}</Series>");
        sb.AppendLine($"  <Title>{SecurityElement.Escape(title)}</Title>");
        if (!string.IsNullOrEmpty(number))
        {
            sb.AppendLine($"  <Number>{SecurityElement.Escape(number)}</Number>");
        }
        sb.AppendLine($"  <PageCount>{pageCount}</PageCount>");
        sb.AppendLine("</ComicInfo>");
        return sb.ToString();
    }
}
