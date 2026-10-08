using MangaDl.Core.Local;
using Xunit;

namespace MangaDl.Core.Tests;

/// <summary>Ports the worked examples straight out of frontend/src/lib/archiveInspector.ts's
/// own doc comments, so a change here that breaks parity with web is caught directly.</summary>
public class ArchiveInspectorTests
{
    [Fact]
    public void NaturalSort_OrdersChapterTwoBeforeChapterTen()
    {
        var list = new List<string> { "Chapter 10", "Chapter 2", "Chapter 1" };
        list.Sort(ArchiveInspector.NaturalSortComparer.Instance);
        Assert.Equal(["Chapter 1", "Chapter 2", "Chapter 10"], list);
    }

    [Fact]
    public void NaturalSort_OrdersPageFilenamesNumerically()
    {
        var list = new List<string> { "page10.jpg", "page1.jpg", "page2.jpg" };
        list.Sort(ArchiveInspector.NaturalSortComparer.Instance);
        Assert.Equal(["page1.jpg", "page2.jpg", "page10.jpg"], list);
    }

    [Fact]
    public void ParseArchiveFilename_RangeExample_MatchesDocComment()
    {
        // "Solo_Leveling_01-10.zip" -> { seriesTitle: "Solo Leveling", rangeStart: 1, rangeEnd: 10 }
        var meta = ArchiveInspector.ParseArchiveFilename("Solo_Leveling_01-10.zip");
        Assert.Equal("Solo Leveling", meta.SeriesTitle);
        Assert.Equal(1, meta.RangeStart);
        Assert.Equal(10, meta.RangeEnd);
        Assert.True(meta.IsRange);
    }

    [Fact]
    public void ParseArchiveFilename_SingleChapterExample_MatchesDocComment()
    {
        // "One_Piece_Chapter_1080.cbz" -> { seriesTitle: "One Piece", rangeStart: 1080, rangeEnd: 1080 }
        var meta = ArchiveInspector.ParseArchiveFilename("One_Piece_Chapter_1080.cbz");
        Assert.Equal("One Piece", meta.SeriesTitle);
        Assert.Equal(1080, meta.RangeStart);
        Assert.Equal(1080, meta.RangeEnd);
        Assert.False(meta.IsRange);
    }

    [Fact]
    public void ParseArchiveFilename_StripsGroupAndVolumeTags()
    {
        var meta = ArchiveInspector.ParseArchiveFilename("[TeamName] My Series (v01) - c003.cbz");
        Assert.DoesNotContain("TeamName", meta.SeriesTitle);
        Assert.DoesNotContain("v01", meta.SeriesTitle);
    }

    [Fact]
    public void ParseArchiveFilename_NoDigitsAtAll_LeavesTitleUnchanged()
    {
        var meta = ArchiveInspector.ParseArchiveFilename("My Series.cbz");
        Assert.Equal("My Series", meta.SeriesTitle);
        Assert.Null(meta.RangeStart);
        Assert.False(meta.IsRange);
    }

    [Theory]
    [InlineData("Chapter 1", 1.0)]
    [InlineData("Ch. 02", 2.0)]
    [InlineData("10", 10.0)]
    [InlineData("c05.5", 5.5)]
    public void ExtractChapterNumber_MatchesDocComment(string name, double expected)
    {
        Assert.Equal(expected, ArchiveInspector.ExtractChapterNumber(name, fallback: -1));
    }
}
