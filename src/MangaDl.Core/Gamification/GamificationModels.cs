namespace MangaDl.Core.Gamification;

public sealed record HunterRank(
    string Code,
    string Name,
    string Tag,
    string Tier,
    string Color,
    string BgColor,
    int MinScore,
    int NextScore);

public sealed record MilestoneBadge(
    string Id,
    string Title,
    string Category,
    int Threshold,
    string Description,
    string Tier,
    string Color);

public sealed record MilestoneStatus(
    MilestoneBadge Badge,
    int CurrentValue,
    bool Unlocked,
    double ProgressPercentage);

public sealed record GamificationProfile(
    int Score,
    HunterRank Rank,
    HunterRank? NextRank,
    double ProgressToNextRank,
    int ChaptersRead,
    int StreakDays,
    int LibraryCount,
    IReadOnlyList<MilestoneStatus> Milestones);
