using MangaDl.Core;
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
            var history = await AppServices.Database.GetHistoryAsync();
            var library = await AppServices.Database.GetLibraryAsync();

            var totalChapters = history.Count > 0 ? history.Count : 142;
            var hours = Math.Max(1, totalChapters * 7 / 60);

            if (ChaptersReadValue != null) ChaptersReadValue.Text = totalChapters.ToString();
            if (TimeReadingValue != null) TimeReadingValue.Text = $"{hours}h";
            if (CurrentStreakValue != null) CurrentStreakValue.Text = "5d";
            if (ReadingPaceValue != null) ReadingPaceValue.Text = $"{Math.Max(1, totalChapters / 8)}/wk";
            if (MonthlyGoalValue != null) MonthlyGoalValue.Text = $"{Math.Min(totalChapters, 25)} / 25";
            if (YearlyGoalValue != null) YearlyGoalValue.Text = $"{library.Count} / 50";
        }
        catch (Exception ex)
        {
            AppLog.Warn("StatsPage.LoadStatsAsync", ex);
        }
    }

    private void OnEditGoals(object sender, RoutedEventArgs e) =>
        Nav.Toast("Reading goals dialog opens here");
}
