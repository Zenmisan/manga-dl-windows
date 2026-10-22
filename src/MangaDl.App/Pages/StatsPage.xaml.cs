using System.Collections.ObjectModel;
using MangaDl.Core;
using MangaDl.Core.Gamification;
using MangaDl.Helpers;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages;

/// <summary>One day in the activity heatmap (0–3 intensity).</summary>
public sealed record HeatDay(int Level)
{
    public int Level { get; set; } = Level;
}

public sealed partial class StatsPage : Page
{
    public ObservableCollection<HeatDay> Activity { get; } = [];
    public ObservableCollection<Bar> ByCategory { get; } = [];
    public ObservableCollection<Bar> BySource { get; } = [];

    public StatsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadStatsAsync();
    }

    private async Task LoadStatsAsync()
    {
        try
        {
            var history = await AppServices.Database.GetHistoryAsync(500);
            var library = await AppServices.Database.GetLibraryAsync();
            var completed = await AppServices.Database.GetAllCompletedProgressAsync();

            var totalChapters = Math.Max(completed.Count, history.Count);
            var hours = totalChapters * 7 / 60;

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
                streak = 0;
            }

            if (ChaptersReadValue != null) ChaptersReadValue.Text = totalChapters.ToString();
            if (TimeReadingValue != null) TimeReadingValue.Text = $"{hours}h";
            if (CurrentStreakValue != null) CurrentStreakValue.Text = $"{streak}d";
            if (ReadingPaceValue != null) ReadingPaceValue.Text = $"{Math.Max(1, totalChapters / 8)}/wk";

            // Reading goals from settings
            var mGoal = Math.Max(1, AppServices.Settings.MonthlyGoal);
            var yGoal = Math.Max(1, AppServices.Settings.YearlyGoal);
            if (MonthlyGoalValue != null) MonthlyGoalValue.Text = $"{Math.Min(totalChapters, mGoal)} / {mGoal}";
            if (YearlyGoalValue != null) YearlyGoalValue.Text = $"{Math.Min(library.Count, yGoal)} / {yGoal}";

            // Activity heatmap (52 weeks = 364 days)
            var historyCountsByDate = history
                .GroupBy(h => DateTimeOffset.FromUnixTimeMilliseconds(h.ReadAt).ToLocalTime().Date)
                .ToDictionary(g => g.Key, g => g.Count());

            Activity.Clear();
            var startDate = DateTime.Today.AddDays(-363);
            for (var d = startDate; d <= DateTime.Today; d = d.AddDays(1))
            {
                historyCountsByDate.TryGetValue(d, out var count);
                var level = count switch
                {
                    0 => 0,
                    1 or 2 => 1,
                    >= 3 and <= 5 => 2,
                    _ => 3
                };
                Activity.Add(new HeatDay(level));
            }

            // By Category
            ByCategory.Clear();
            var cats = await AppServices.Database.GetCategoriesAsync();
            if (cats.Count > 0 && library.Count > 0)
            {
                var total = (double)library.Count;
                foreach (var cat in cats)
                {
                    var itemsInCat = await AppServices.Database.GetMangaInCategoriesAsync(cat.Id);
                    if (itemsInCat.Count > 0)
                    {
                        ByCategory.Add(new Bar(cat.Name, itemsInCat.Count / total));
                    }
                }
            }
            if (ByCategory.Count == 0 && library.Count > 0)
            {
                var typeGroups = library.GroupBy(m => string.IsNullOrWhiteSpace(m.Type) ? "Manga" : m.Type).ToList();
                var total = (double)library.Count;
                foreach (var g in typeGroups)
                {
                    ByCategory.Add(new Bar(g.Key, g.Count() / total));
                }
            }
            if (ByCategory.Count == 0)
            {
                ByCategory.Add(new Bar("Default", 1.0));
            }

            // By Source
            BySource.Clear();
            if (library.Count > 0)
            {
                var srcGroups = library.GroupBy(m => m.Provider).OrderByDescending(g => g.Count()).Take(5).ToList();
                var total = (double)library.Count;
                foreach (var g in srcGroups)
                {
                    BySource.Add(new Bar(g.Key, g.Count() / total));
                }
            }
            else
            {
                BySource.Add(new Bar("No sources yet", 0.0));
            }

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

    private async void OnEditGoals(object sender, RoutedEventArgs e)
    {
        var mBox = new TextBox
        {
            Text = AppServices.Settings.MonthlyGoal.ToString(),
            Margin = new Thickness(0, 0, 0, 12)
        };
        var yBox = new TextBox
        {
            Text = AppServices.Settings.YearlyGoal.ToString()
        };

        var panel = new StackPanel { Width = 300, Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = "Monthly Chapter Goal", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 13 });
        panel.Children.Add(mBox);
        panel.Children.Add(new TextBlock { Text = "Yearly Manga Goal", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 13 });
        panel.Children.Add(yBox);

        var dialog = new ContentDialog
        {
            Title = "Edit Reading Goals",
            Content = panel,
            PrimaryButtonText = "Save Goals",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            if (int.TryParse(mBox.Text.Trim(), out var m) && m > 0)
                AppServices.Settings.MonthlyGoal = m;
            if (int.TryParse(yBox.Text.Trim(), out var y) && y > 0)
                AppServices.Settings.YearlyGoal = y;

            AppServices.Settings.Save();
            await LoadStatsAsync();
            Nav.Toast("Reading goals updated");
        }
    }
}
