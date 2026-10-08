using MangaDl.Core.Gamification;

namespace MangaDl.Core.Tests;

public class GamificationTests
{
    [Fact]
    public void CalculateScore_CalculatesAccurately()
    {
        // 10 ch * 10 = 100; 2 streak * 50 = 100; 4 manga * 25 = 100 => 300
        var score = GamificationService.CalculateScore(10, 2, 4);
        Assert.Equal(300, score);

        var zero = GamificationService.CalculateScore(0, 0, 0);
        Assert.Equal(0, zero);
    }

    [Theory]
    [InlineData(0, "E", "E-Rank Novice")]
    [InlineData(499, "E", "E-Rank Novice")]
    [InlineData(500, "D", "D-Rank Hunter")]
    [InlineData(1499, "D", "D-Rank Hunter")]
    [InlineData(1500, "C", "C-Rank Hunter")]
    [InlineData(3999, "C", "C-Rank Hunter")]
    [InlineData(4000, "B", "B-Rank Hunter")]
    [InlineData(7999, "B", "B-Rank Hunter")]
    [InlineData(8000, "A", "A-Rank Hunter")]
    [InlineData(14999, "A", "A-Rank Hunter")]
    [InlineData(15000, "S", "S-Rank Hunter")]
    [InlineData(25000, "MONARCH", "Shadow Monarch")]
    [InlineData(50000, "MONARCH", "Shadow Monarch")]
    public void GetHunterRank_ReturnsCorrectRank(int score, string expectedCode, string expectedName)
    {
        var rank = GamificationService.GetHunterRank(score);
        Assert.Equal(expectedCode, rank.Code);
        Assert.Equal(expectedName, rank.Name);
    }

    [Fact]
    public void ProgressToNext_CalculatesCorrectPercentage()
    {
        // D-Rank is 500 to 1500. Score 1000 is 50% (500/1000)
        var rank = GamificationService.GetHunterRank(1000);
        var next = GamificationService.GetNextRank(rank);
        Assert.NotNull(next);
        Assert.Equal("C", next.Code);

        var progress = GamificationService.CalculateProgressToNext(1000, rank, next);
        Assert.Equal(0.5, progress, 2);

        // Monarch has no next rank
        var monarch = GamificationService.GetHunterRank(30000);
        var monarchNext = GamificationService.GetNextRank(monarch);
        Assert.Null(monarchNext);
        var monarchProgress = GamificationService.CalculateProgressToNext(30000, monarch, monarchNext);
        Assert.Equal(1.0, monarchProgress);
    }

    [Fact]
    public void EvaluateMilestones_UnlocksBadgesCorrectly()
    {
        // 50 chapters read, 7 streak, 10 library
        var milestones = GamificationService.EvaluateMilestones(chaptersRead: 50, streakDays: 7, libraryCount: 10);

        var firstPage = milestones.First(m => m.Badge.Id == "first_page");
        Assert.True(firstPage.Unlocked);
        Assert.Equal(1.0, firstPage.ProgressPercentage);

        var enthusiast = milestones.First(m => m.Badge.Id == "manga_enthusiast");
        Assert.True(enthusiast.Unlocked);

        var volumeDevourer = milestones.First(m => m.Badge.Id == "volume_devourer");
        Assert.False(volumeDevourer.Unlocked);
        Assert.Equal(0.5, volumeDevourer.ProgressPercentage);

        var consistent = milestones.First(m => m.Badge.Id == "consistent");
        Assert.True(consistent.Unlocked);

        var curator = milestones.First(m => m.Badge.Id == "curator");
        Assert.True(curator.Unlocked);
    }

    [Fact]
    public void GetProfile_ReturnsCompleteProfile()
    {
        var profile = GamificationService.GetProfile(chaptersRead: 150, streakDays: 5, libraryCount: 20);
        Assert.NotNull(profile.Rank);
        Assert.True(profile.Score > 0);
        Assert.Equal(150, profile.ChaptersRead);
        Assert.Equal(5, profile.StreakDays);
        Assert.Equal(20, profile.LibraryCount);
        Assert.NotEmpty(profile.Milestones);
    }
}
