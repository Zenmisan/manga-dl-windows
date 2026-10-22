using MangaDl.Core.Database;
using MangaDl.Core.Database.Entities;
using MangaDl.Core.Gamification;

namespace MangaDl.Core.Community;

public sealed class CommunityService
{
    private static readonly (string Name, string Avatar, int Chapters, int Streak, int Library, string Badge)[] CommunityHunters =
    [
        ("ShadowMonarch_99", "", 2840, 142, 118, "Grandmaster Reader"),
        ("ThomasTheGoliath", "", 2410, 88, 94, "Mythic Sage"),
        ("SwordDancer_Cha", "", 2150, 115, 82, "Arc Conqueror"),
        ("Liu_Zhigang", "", 1980, 64, 76, "Binge Specialist"),
        ("Baek_BeastMode", "", 1720, 45, 68, "Volume Devourer"),
        ("Choi_FlameMaster", "", 1580, 52, 60, "Panel Virtuoso"),
        ("Go_Gunhee_Legacy", "", 1490, 78, 85, "Archivist"),
        ("Iron_Tank_Hunter", "", 1320, 36, 48, "Binge Specialist"),
        ("Kanna_Kamui_Reader", "", 1210, 41, 52, "Arc Conqueror"),
        ("SoloLeveler_Max", "", 1050, 30, 42, "Grandmaster Reader"),
        ("WebtoonPhantom", "", 980, 28, 38, "Panel Virtuoso"),
        ("DungeonScavenger", "", 840, 21, 35, "Binge Specialist"),
        ("SpeedReader_9000", "", 760, 19, 30, "Volume Devourer"),
        ("NightOwl_Otaku", "", 690, 14, 28, "Manga Enthusiast"),
        ("PanelSurfer", "", 580, 12, 22, "Casual Reader"),
    ];

    public List<LeaderboardEntry> GetLeaderboard(
        int userScore,
        int userChapters,
        int userStreak,
        string username = "You",
        string? userAvatar = null,
        string timeframe = "all_time")
    {
        double multiplier = timeframe switch
        {
            "weekly" => 0.08,
            "monthly" => 0.35,
            _ => 1.0
        };

        var list = new List<LeaderboardEntry>();

        foreach (var h in CommunityHunters)
        {
            var adjChapters = (int)(h.Chapters * multiplier);
            var adjStreak = (int)(h.Streak * Math.Min(1.0, multiplier * 1.5));
            var score = GamificationService.CalculateScore(adjChapters, adjStreak, h.Library);
            var rank = GamificationService.GetHunterRank(score);

            list.Add(new LeaderboardEntry(
                Rank: 0,
                Username: h.Name,
                AvatarUrl: h.Avatar,
                HunterRank: rank,
                Score: score,
                ChaptersRead: adjChapters,
                StreakDays: adjStreak,
                BadgeTitle: h.Badge,
                IsCurrentUser: false));
        }

        // Add local user
        var adjustedUserScore = (int)(userScore * multiplier);
        var adjustedUserChapters = (int)(userChapters * multiplier);
        var adjustedUserStreak = (int)(userStreak * Math.Min(1.0, multiplier * 1.5));
        var userHunterRank = GamificationService.GetHunterRank(adjustedUserScore);

        list.Add(new LeaderboardEntry(
            Rank: 0,
            Username: username,
            AvatarUrl: userAvatar,
            HunterRank: userHunterRank,
            Score: adjustedUserScore,
            ChaptersRead: adjustedUserChapters,
            StreakDays: adjustedUserStreak,
            BadgeTitle: "Active Reader",
            IsCurrentUser: true));

        // Sort descending by score
        var sorted = list
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.ChaptersRead)
            .ToList();

        var rankedList = new List<LeaderboardEntry>(sorted.Count);
        for (var i = 0; i < sorted.Count; i++)
        {
            rankedList.Add(sorted[i] with { Rank = i + 1 });
        }

        return rankedList;
    }

    public async Task<List<CommentItem>> GetCommentsAsync(MangaDatabase db, string provider, string mangaId, string? chapterId = null)
    {
        var entities = await db.GetCommentsAsync(provider, mangaId, chapterId);
        if (entities.Count == 0 && string.IsNullOrEmpty(chapterId))
        {
            // Seed sample discussion comments if none exist
            await SeedDefaultCommentsAsync(db, provider, mangaId);
            entities = await db.GetCommentsAsync(provider, mangaId, chapterId);
        }

        return entities.Select(e => new CommentItem(
            e.Id,
            e.Provider,
            e.MangaId,
            e.ChapterId,
            e.Username,
            e.UserAvatar,
            e.HunterRank ?? "Novice",
            e.Content,
            e.CreatedAt,
            e.Likes,
            e.IsLiked == 1
        )).ToList();
    }

    public async Task<CommentItem> PostCommentAsync(
        MangaDatabase db,
        string provider,
        string mangaId,
        string? chapterId,
        string username,
        string? userAvatar,
        string hunterRank,
        string content)
    {
        var entity = new CommentEntity
        {
            Provider = provider,
            MangaId = mangaId,
            ChapterId = chapterId,
            Username = string.IsNullOrWhiteSpace(username) ? "Anonymous Hunter" : username,
            UserAvatar = userAvatar,
            HunterRank = string.IsNullOrWhiteSpace(hunterRank) ? "Novice" : hunterRank,
            Content = content.Trim(),
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Likes = 0,
            IsLiked = 0
        };

        await db.AddCommentAsync(entity);

        return new CommentItem(
            entity.Id,
            entity.Provider,
            entity.MangaId,
            entity.ChapterId,
            entity.Username,
            entity.UserAvatar,
            entity.HunterRank,
            entity.Content,
            entity.CreatedAt,
            entity.Likes,
            false);
    }

    public async Task<bool> LikeCommentAsync(MangaDatabase db, string commentId)
    {
        return await db.LikeCommentAsync(commentId);
    }

    private static async Task SeedDefaultCommentsAsync(MangaDatabase db, string provider, string mangaId)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var seeds = new[]
        {
            new CommentEntity
            {
                Provider = provider,
                MangaId = mangaId,
                ChapterId = null,
                Username = "ShadowHunter",
                HunterRank = "A-Rank Hunter",
                Content = "The art and pacing in this series are incredible. Every chapter keeps you on the edge of your seat!",
                CreatedAt = now - (3600 * 1000 * 18),
                Likes = 14,
                IsLiked = 0
            },
            new CommentEntity
            {
                Provider = provider,
                MangaId = mangaId,
                ChapterId = null,
                Username = "PanelVirtuoso",
                HunterRank = "S-Rank Hunter",
                Content = "Definite masterpiece status. If you haven't read ahead in the novel, you're in for some wild plot twists.",
                CreatedAt = now - (3600 * 1000 * 42),
                Likes = 9,
                IsLiked = 0
            }
        };

        foreach (var s in seeds)
        {
            await db.AddCommentAsync(s);
        }
    }
}
