using System.Text.RegularExpressions;

namespace MangaDl.Core.Local;

/// <summary>Shared by DownloadManager and LocalImportService so filesystem-safe
/// naming stays consistent between downloaded and locally-imported chapters.</summary>
public static class FileNaming
{
    public static string SanitizeFileName(string name)
    {
        var invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
        var regex = new Regex($"[{Regex.Escape(invalid)}]");
        var clean = regex.Replace(name, "_").Trim();
        return string.IsNullOrEmpty(clean) ? "untitled" : clean;
    }

    /// <summary>Lowercase, alnum-and-dash slug for use as a manga_id.</summary>
    public static string Slugify(string name)
    {
        var lowered = name.ToLowerInvariant().Trim();
        var slug = Regex.Replace(lowered, @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "untitled" : slug;
    }
}
