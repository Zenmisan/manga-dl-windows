using MangaDl.Core.Community;
using MangaDl.Core.Database;
using MangaDl.Core.Database.Entities;
using MangaDl.Core.Gamification;

namespace MangaDl.Core.Tests;

public class CommunityTests : IAsyncLifetime
{
    private readonly string _dbPath;
    private readonly MangaDatabase _db;
    private readonly CommunityService _service;

    public CommunityTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"mangadl_test_community_{Guid.NewGuid():N}.db");
        _db = new MangaDatabase(_dbPath);
        _service = new CommunityService();
    }

    public Task InitializeAsync() => _db.InitializeAsync();

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task AddAndGetComments_FiltersByChapterAndSeries()
    {
        const string provider = "asura";
        const string mangaId = "solo-leveling";

        // Series-level comment
        var c1 = await _service.PostCommentAsync(
            _db, provider, mangaId, chapterId: null,
            username: "HunterJin", userAvatar: null,
            hunterRank: "S-Rank Hunter", content: "Masterpiece series from start to finish!");

        // Chapter 1 comment
        var c2 = await _service.PostCommentAsync(
            _db, provider, mangaId, chapterId: "ch-1",
            username: "NoviceReader", userAvatar: null,
            hunterRank: "E-Rank Novice", content: "The double dungeon incident begins...");

        // Query series comments
        var seriesComments = await _service.GetCommentsAsync(_db, provider, mangaId, chapterId: null);
        Assert.Contains(seriesComments, c => c.Id == c1.Id);

        // Query chapter 1 comments
        var ch1Comments = await _service.GetCommentsAsync(_db, provider, mangaId, chapterId: "ch-1");
        Assert.Single(ch1Comments);
        Assert.Equal("The double dungeon incident begins...", ch1Comments[0].Content);
        Assert.Equal("NoviceReader", ch1Comments[0].Username);

        // Query chapter 2 (empty)
        var ch2Comments = await _service.GetCommentsAsync(_db, provider, mangaId, chapterId: "ch-2");
        Assert.Empty(ch2Comments);
    }

    [Fact]
    public async Task LikeComment_TogglesAndIncrementsCount()
    {
        const string provider = "flamecomics";
        const string mangaId = "omniscient-reader";

        var comment = await _service.PostCommentAsync(
            _db, provider, mangaId, chapterId: "ch-10",
            username: "DokjaFan", userAvatar: null,
            hunterRank: "A-Rank Hunter", content: "Three ways to survive an apocalypse!");

        Assert.Equal(0, comment.Likes);
        Assert.False(comment.IsLiked);

        // Like it
        var isLiked = await _service.LikeCommentAsync(_db, comment.Id);
        Assert.True(isLiked);

        var list = await _service.GetCommentsAsync(_db, provider, mangaId, "ch-10");
        Assert.Single(list);
        Assert.Equal(1, list[0].Likes);
        Assert.True(list[0].IsLiked);

        // Unlike it
        var unliked = await _service.LikeCommentAsync(_db, comment.Id);
        Assert.False(unliked);

        var list2 = await _service.GetCommentsAsync(_db, provider, mangaId, "ch-10");
        Assert.Equal(0, list2[0].Likes);
        Assert.False(list2[0].IsLiked);
    }

    [Fact]
    public void Leaderboard_ComputesRankingsAndPlacesUser()
    {
        // User with high achievements
        var userScore = GamificationService.CalculateScore(chaptersRead: 2500, streakDays: 120, libraryCount: 80);
        var entries = _service.GetLeaderboard(userScore, userChapters: 2500, userStreak: 120, username: "PlayerOne");

        Assert.NotEmpty(entries);
        Assert.Equal(1, entries[0].Rank);

        // The user should be present
        var userEntry = entries.FirstOrDefault(e => e.IsCurrentUser);
        Assert.NotNull(userEntry);
        Assert.Equal("PlayerOne", userEntry.Username);
        Assert.True(userEntry.Rank > 0 && userEntry.Rank <= entries.Count);

        // List must be sorted descending by score
        for (var i = 1; i < entries.Count; i++)
        {
            Assert.True(entries[i - 1].Score >= entries[i].Score);
            Assert.Equal(i + 1, entries[i].Rank);
        }
    }

    [Fact]
    public void Leaderboard_TimeframeFiltering_ScalesScoresPredictably()
    {
        var allTime = _service.GetLeaderboard(userScore: 5000, userChapters: 400, userStreak: 30, timeframe: "all_time");
        var weekly = _service.GetLeaderboard(userScore: 5000, userChapters: 400, userStreak: 30, timeframe: "weekly");

        var allTimeTop = allTime[0].Score;
        var weeklyTop = weekly[0].Score;

        Assert.True(weeklyTop < allTimeTop);
    }
}
