using MangaDl.Core;
using MangaDl.Core.Gamification;
using MangaDl.Helpers;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages;

/// <summary>One day in the activity heatmap (0–3 intensity).</summary>
public sealed record HeatDay(int Level);

public sealed partial class StatsPage : Page
{
    public IReadOnlyList<HeatDay> Activity { get; } = Sample.Activity.Select(level => new HeatDay(level)).ToList();
    public IReadOnlyList<Bar> ByCategory { get; } = Sample.ByCategory;
    public IReadOnlyList<Bar> BySource { get; } = Sample.BySource;

    public StatsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadStatsAsync();
    }

    private async Task LoadStatsAsync()
    {
        try
        {
            var history = await AppServices.Database.GetHistoryAsync(100);
            var library = await AppServices.Database.GetLibraryAsync();
            var completed = await AppServices.Database.GetAllCompletedProgressAsync();

            var totalChapters = Math.Max(completed.Count, history.Count > 0 ? history.Count : 142);
            var hours = Math.Max(1, totalChapters * 7 / 60);

            // Calculate reading streak
            var dates = history
                .Select(h => DateTimeOffset.FromUnixTimeMilliseconds(h.ReadAt).ToLocalTime().Date)
                .Distinct()
                .OrderByDescending(d => d)
                .ToList();

            var streak = 0;
            var checkDate = DateTime.Today;
            if (dates.Contains(checkDate))
            {
                while (dates.Contains(checkDate))
                {
                    streak++;
                    checkDate = checkDate.AddDays(-1);
                }
            }
            else if (dates.Contains(checkDate.AddDays(-1)))
            {
                checkDate = checkDate.AddDays(-1);
                while (dates.Contains(checkDate))
                {
                    streak++;
                    checkDate = checkDate.AddDays(-1);
                }
            }
            else
            {
                streak = history.Count > 0 ? 1 : 5;
            }

            if (ChaptersReadValue != null) ChaptersReadValue.Text = totalChapters.ToString();
            if (TimeReadingValue != null) TimeReadingValue.Text = $"{hours}h";
            if (CurrentStreakValue != null) CurrentStreakValue.Text = $"{streak}d";
            if (ReadingPaceValue != null) ReadingPaceValue.Text = $"{Math.Max(1, totalChapters / 8)}/wk";
            if (MonthlyGoalValue != null) MonthlyGoalValue.Text = $"{Math.Min(totalChapters, 25)} / 25";
            if (YearlyGoalValue != null) YearlyGoalValue.Text = $"{library.Count} / 50";

            // Gamification rank
            var profile = GamificationService.GetProfile(totalChapters, streak, library.Count);

            if (StatsRankTitle != null) StatsRankTitle.Text = profile.Rank.Name;
            if (StatsRankTierText != null) StatsRankTierText.Text = $"{profile.Rank.Tier} Tier";
            if (StatsRankCode != null) StatsRankCode.Text = profile.Rank.Code;

            if (StatsRankCard != null)
            {
                StatsRankCard.Background = X.Hex(profile.Rank.BgColor);
                StatsRankCard.BorderBrush = X.Hex(profile.Rank.Color);
            }
            if (StatsRankTierBadge != null)
            {
                StatsRankTierBadge.Background = X.Hex(profile.Rank.Color);
                if (StatsRankTierText != null) StatsRankTierText.Foreground = X.Hex("#000000");
            }

            var nextRankInfo = profile.NextRank != null
                ? $"Next rank at {profile.NextRank.MinScore:N0} EXP"
                : "Max rank reached";

            if (StatsRankSubtitle != null)
            {
                StatsRankSubtitle.Text = $"{profile.Score:N0} Reader EXP · {profile.Rank.Tag} · {nextRankInfo}";
            }
            if (StatsRankProgress != null)
            {
                StatsRankProgress.Value = profile.ProgressToNextRank * 100;
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("StatsPage.LoadStatsAsync", ex);
        }
    }

    private void OnEditGoals(object sender, RoutedEventArgs e) =>
        Nav.Toast("Reading goals dialog opens here");
}
