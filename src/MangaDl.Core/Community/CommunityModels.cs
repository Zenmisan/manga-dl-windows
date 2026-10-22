using MangaDl.Core.Gamification;

namespace MangaDl.Core.Community;

public sealed record LeaderboardEntry(
    int Rank,
    string Username,
    string? AvatarUrl,
    HunterRank HunterRank,
    int Score,
    int ChaptersRead,
    int StreakDays,
    string? BadgeTitle,
    bool IsCurrentUser = false);

public sealed record CommentItem(
    string Id,
    string Provider,
    string MangaId,
    string? ChapterId,
    string Username,
    string? UserAvatar,
    string HunterRank,
    string Content,
    long CreatedAt,
    int Likes,
    bool IsLiked);
