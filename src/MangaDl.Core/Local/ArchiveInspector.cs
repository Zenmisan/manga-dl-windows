using System.Text.RegularExpressions;

namespace MangaDl.Core.Local;

public sealed record ArchiveFilenameMeta(string SeriesTitle, double? RangeStart, double? RangeEnd, bool IsRange);

/// <summary>
/// Ports frontend/src/lib/archiveInspector.ts — the web app's local CBZ/ZIP importer.
/// Same filename-based chapter/range detection, same natural sort, same directory-
/// structure patterns (one archive can bundle several chapters as subfolders).
/// </summary>
public static class ArchiveInspector
{
    public static readonly HashSet<string> ValidImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp", ".avif"
    };

    /// <summary>Natural sort: "Chapter 2" sorts before "Chapter 10". Splits into
    /// alternating digit/non-digit runs and compares numerically where both sides
    /// are numeric, matching JS's localeCompare(..., {numeric:true}).</summary>
    public sealed class NaturalSortComparer : IComparer<string>
    {
        public static readonly NaturalSortComparer Instance = new();

        public int Compare(string? a, string? b)
        {
            a ??= ""; b ??= "";
            var ai = 0; var bi = 0;
            while (ai < a.Length && bi < b.Length)
            {
                if (char.IsDigit(a[ai]) && char.IsDigit(b[bi]))
                {
                    var aStart = ai; while (ai < a.Length && char.IsDigit(a[ai])) ai++;
                    var bStart = bi; while (bi < b.Length && char.IsDigit(b[bi])) bi++;
                    var aNum = a[aStart..ai].TrimStart('0');
                    var bNum = b[bStart..bi].TrimStart('0');
                    if (aNum.Length != bNum.Length) return aNum.Length - bNum.Length;
                    var cmp = string.CompareOrdinal(aNum, bNum);
                    if (cmp != 0) return cmp;
                }
                else
                {
                    var cmp = char.ToLowerInvariant(a[ai]).CompareTo(char.ToLowerInvariant(b[bi]));
                    if (cmp != 0) return cmp;
                    ai++; bi++;
                }
            }
            return (a.Length - ai) - (b.Length - bi);
        }
    }

    /// <summary>"Solo_Leveling_01-10.zip" -> ("Solo Leveling", 1, 10, true).
    /// "One_Piece_Chapter_1080.cbz" -> ("One Piece", 1080, 1080, false).</summary>
    public static ArchiveFilenameMeta ParseArchiveFilename(string filename)
    {
        var clean = Regex.Replace(filename, @"\.(cbz|zip|epub|cbr)$", "", RegexOptions.IgnoreCase);

        var rangeMatch = Regex.Match(clean, @"(?:ch(?:apter)?\.?\s*|c)?(\d+)\s*[-–_to]+\s*(?:ch(?:apter)?\.?\s*|c)?(\d+)", RegexOptions.IgnoreCase);
        double? rangeStart = null, rangeEnd = null;
        if (rangeMatch.Success)
        {
            rangeStart = double.Parse(rangeMatch.Groups[1].Value);
            rangeEnd = double.Parse(rangeMatch.Groups[2].Value);
            if (rangeStart > rangeEnd) (rangeStart, rangeEnd) = (rangeEnd, rangeStart);
        }

        var singleMatch = Regex.Match(clean, @"(?:ch(?:apter)?\.?\s*|c)(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
        if (!singleMatch.Success)
            singleMatch = Regex.Match(clean, @"(?:^|[^\d])(\d+(?:\.\d+)?)(?:$|[^\d])", RegexOptions.IgnoreCase);
        double? singleCh = singleMatch.Success ? double.Parse(singleMatch.Groups[1].Value) : null;

        var seriesTitle = clean;
        if (rangeMatch.Success && rangeMatch.Index > 0)
        {
            seriesTitle = clean[..rangeMatch.Index];
        }
        else if (singleMatch.Success && singleMatch.Index > 0)
        {
            seriesTitle = clean[..singleMatch.Index];
        }

        seriesTitle = Regex.Replace(seriesTitle, @"[\[\(][^\]\)]*[\]\)]", " "); // strip [Group] / (v01)
        seriesTitle = Regex.Replace(seriesTitle, @"[-_.]+", " ").Trim();

        return new ArchiveFilenameMeta(
            string.IsNullOrEmpty(seriesTitle) ? clean : seriesTitle,
            rangeStart ?? singleCh,
            rangeEnd ?? singleCh,
            rangeMatch.Success);
    }

    /// <summary>Chapter number from a folder/file name, e.g. "Chapter 1" -> 1, "c05.5" -> 5.5.</summary>
    public static double ExtractChapterNumber(string name, double fallback = 1)
    {
        var match = Regex.Match(name, @"(?:ch(?:apter)?\.?\s*|c)?(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
        if (match.Success && double.TryParse(match.Groups[1].Value, out var parsed)) return parsed;
        return fallback;
    }

    /// <summary>Cleans a directory name into a display title ("Chapter_05" -> "Chapter 05"),
    /// falling back to "Chapter {n}" when nothing meaningful is left.</summary>
    public static string CleanDirTitle(string dirName, double chapterNumber)
    {
        var cleaned = Regex.Replace(dirName, @"[-_]+", " ").Trim();
        return cleaned.Length > 0 ? cleaned : $"Chapter {FormatChapterNumber(chapterNumber)}";
    }

    public static string FormatChapterNumber(double n) => n == Math.Floor(n) ? ((long)n).ToString() : n.ToString("0.#");
}
