namespace MangaDl.Core.Gamification;

public static class GamificationService
{
    public static readonly IReadOnlyList<HunterRank> Ranks =
    [
        new("E", "E-Rank Novice", "Awakened Novice", "Bronze", "#71717A", "#27272A", 0, 500),
        new("D", "D-Rank Hunter", "Dungeon Scavenger", "Bronze", "#CD7F32", "#3A2016", 500, 1500),
        new("C", "C-Rank Hunter", "Raid Ready", "Silver", "#94A3B8", "#1E293B", 1500, 4000),
        new("B", "B-Rank Hunter", "Veteran", "Gold", "#F59E0B", "#3B2A10", 4000, 8000),
        new("A", "A-Rank Hunter", "High Guild", "Platinum", "#38BDF8", "#0C2A4A", 8000, 15000),
        new("S", "S-Rank Hunter", "Apex Elite", "Diamond", "#A855F7", "#2E1065", 15000, 25000),
        new("MONARCH", "Shadow Monarch", "#1 Sovereign", "Mythic", "#C084FC", "#3B0764", 25000, int.MaxValue),
    ];

    public static readonly IReadOnlyList<MilestoneBadge> Milestones =
    [
        // Chapters Read
        new("first_page", "First Page", "chapters", 1, "Took the very first step into the panel multiverse.", "bronze", "#CD7F32"),
        new("page_turner", "Page Turner", "chapters", 10, "Getting hooked on the panel flow and cliffhangers.", "bronze", "#CD7F32"),
        new("casual_reader", "Casual Reader", "chapters", 25, "Finding curiosity in every story arc.", "bronze", "#CD7F32"),
        new("manga_enthusiast", "Manga Enthusiast", "chapters", 50, "A dependable appetite for weekly releases.", "silver", "#94A3B8"),
        new("volume_devourer", "Volume Devourer", "chapters", 100, "Consumed a physical shelf worth of tankōbon.", "silver", "#94A3B8"),
        new("arc_conqueror", "Arc Conqueror", "chapters", 250, "Cruised through epic sagas without blinking.", "silver", "#94A3B8"),
        new("binge_specialist", "Binge Specialist", "chapters", 500, "Lost entire weekends to non-stop chapter binges.", "gold", "#F59E0B"),
        new("panel_virtuoso", "Panel Virtuoso", "chapters", 750, "Appreciating every masterstroke and speed line.", "gold", "#F59E0B"),
        new("grandmaster", "Grandmaster Reader", "chapters", 1000, "A thousand chapters traversed across countless realms.", "platinum", "#38BDF8"),
        new("mythic_sage", "Mythic Sage", "chapters", 2500, "Living library of manga and web novels.", "mythic", "#A855F7"),

        // Library Collections
        new("starter_shelf", "Starter Shelf", "library", 1, "Added your very first series to the collection.", "bronze", "#CD7F32"),
        new("curator", "Curator", "library", 10, "Curating a respectable personal reading bookshelf.", "bronze", "#CD7F32"),
        new("collector", "Collector", "library", 25, "An expansive library spanning genres and styles.", "silver", "#94A3B8"),
        new("archivist", "Archivist", "library", 50, "Archiving complete sagas for future generations.", "gold", "#F59E0B"),
        new("grand_librarian", "Grand Librarian", "library", 100, "A towering library worthy of an Alexandria.", "platinum", "#38BDF8"),

        // Reading Streaks
        new("spark", "Daily Spark", "streak", 1, "Read at least one chapter today.", "bronze", "#CD7F32"),
        new("habituated", "Habituated", "streak", 3, "Formed a consistent 3-day reading habit.", "bronze", "#CD7F32"),
        new("consistent", "Consistent", "streak", 7, "A full week of dedicated daily reading.", "silver", "#94A3B8"),
        new("dedicated", "Dedicated", "streak", 14, "Two weeks without missing a single day.", "gold", "#F59E0B"),
        new("unshakeable", "Unshakeable", "streak", 30, "A full month streak. Nothing can distract you.", "platinum", "#38BDF8"),
        new("century_reader", "Century Reader", "streak", 100, "100 consecutive days of immersion in stories.", "mythic", "#A855F7"),
    ];

    public static int CalculateScore(int chaptersRead, int streakDays, int libraryCount) =>
        Math.Max(0, (chaptersRead * 10) + (streakDays * 50) + (libraryCount * 25));

    public static HunterRank GetHunterRank(int score)
    {
        for (var i = Ranks.Count - 1; i >= 0; i--)
        {
            if (score >= Ranks[i].MinScore)
            {
                return Ranks[i];
            }
        }
        return Ranks[0];
    }

    public static HunterRank? GetNextRank(HunterRank currentRank)
    {
        var idx = -1;
        for (var i = 0; i < Ranks.Count; i++)
        {
            if (Ranks[i].Code == currentRank.Code)
            {
                idx = i;
                break;
            }
        }
        return (idx >= 0 && idx < Ranks.Count - 1) ? Ranks[idx + 1] : null;
    }

    public static double CalculateProgressToNext(int score, HunterRank currentRank, HunterRank? nextRank)
    {
        if (nextRank == null || currentRank.NextScore == int.MaxValue) return 1.0;
        var range = nextRank.MinScore - currentRank.MinScore;
        if (range <= 0) return 1.0;
        var currentProgress = score - currentRank.MinScore;
        return Math.Clamp((double)currentProgress / range, 0.0, 1.0);
    }

    public static IReadOnlyList<MilestoneStatus> EvaluateMilestones(int chaptersRead, int streakDays, int libraryCount)
    {
        return Milestones.Select(m =>
        {
            var currentVal = m.Category switch
            {
                "chapters" => chaptersRead,
                "streak" => streakDays,
                "library" => libraryCount,
                _ => 0
            };

            var unlocked = currentVal >= m.Threshold;
            var pct = m.Threshold > 0 ? Math.Clamp((double)currentVal / m.Threshold, 0.0, 1.0) : 1.0;

            return new MilestoneStatus(m, currentVal, unlocked, pct);
        }).ToList();
    }

    public static GamificationProfile GetProfile(int chaptersRead, int streakDays, int libraryCount)
    {
        var score = CalculateScore(chaptersRead, streakDays, libraryCount);
        var rank = GetHunterRank(score);
        var next = GetNextRank(rank);
        var progress = CalculateProgressToNext(score, rank, next);
        var milestones = EvaluateMilestones(chaptersRead, streakDays, libraryCount);

        return new GamificationProfile(
            Score: score,
            Rank: rank,
            NextRank: next,
            ProgressToNextRank: progress,
            ChaptersRead: chaptersRead,
            StreakDays: streakDays,
            LibraryCount: libraryCount,
            Milestones: milestones);
    }
}
