using MangaDl.Core.Community;
using MangaDl.Core.Gamification;
using MangaDl.Helpers;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace MangaDl.Pages;

public sealed partial class LeaderboardPage : Page
{
    private string _currentTimeframe = "all_time";

    public LeaderboardPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Nav.SetTitle("Hunter Hall of Fame");
            _ = LoadLeaderboardAsync();
        };
    }

    private void OnTimeframeChecked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb || rb.Tag is null)
        {
            if (MonthlyOption?.IsChecked == true) _currentTimeframe = "monthly";
            else if (WeeklyOption?.IsChecked == true) _currentTimeframe = "weekly";
            else _currentTimeframe = "all_time";
        }
        else
        {
            _currentTimeframe = rb.Tag.ToString() ?? "all_time";
        }

        _ = LoadLeaderboardAsync();
    }

    private async Task LoadLeaderboardAsync()
    {
        try
        {
            var progress = await AppServices.Database.GetAllCompletedProgressAsync();
            var history = await AppServices.Database.GetHistoryAsync(500);
            var library = await AppServices.Database.GetLibraryAsync();

            var chaptersRead = progress.Count;
            var libraryCount = library.Count;
            var streakDays = CalculateStreakDays(history);

            var userScore = GamificationService.CalculateScore(chaptersRead, streakDays, libraryCount);
            var username = AppServices.Settings.UserName ?? "You";

            var entries = AppServices.Community.GetLeaderboard(
                userScore,
                chaptersRead,
                streakDays,
                username: username,
                userAvatar: null,
                timeframe: _currentTimeframe);

            // 1. Populate User Standing Banner
            var userEntry = entries.FirstOrDefault(e => e.IsCurrentUser);
            if (userEntry != null)
            {
                UserStandingName.Text = userEntry.Username;
                UserPlacementText.Text = $"#{userEntry.Rank}";
                UserAvatarInitial.Text = string.IsNullOrWhiteSpace(userEntry.Username) ? "U" : userEntry.Username[0].ToString().ToUpperInvariant();
                UserRankText.Text = userEntry.HunterRank.Name;
                UserStatsSubtitle.Text = $"{userEntry.Score:N0} Reader EXP · {userEntry.ChaptersRead} chapters read · {userEntry.StreakDays}d streak";

                UserRankBadge.Background = X.Hex(userEntry.HunterRank.Color);
                UserStandingCard.BorderBrush = X.Hex(userEntry.HunterRank.Color);
            }

            // 2. Populate Podium
            if (entries.Count >= 3)
            {
                var top1 = entries[0];
                var top2 = entries[1];
                var top3 = entries[2];

                Podium1Name.Text = top1.Username;
                Podium1Rank.Text = top1.HunterRank.Name;
                Podium1Rank.Foreground = X.Hex(top1.HunterRank.Color);
                Podium1Score.Text = $"{top1.Score:N0} EXP";

                Podium2Name.Text = top2.Username;
                Podium2Rank.Text = top2.HunterRank.Name;
                Podium2Rank.Foreground = X.Hex(top2.HunterRank.Color);
                Podium2Score.Text = $"{top2.Score:N0} EXP";

                Podium3Name.Text = top3.Username;
                Podium3Rank.Text = top3.HunterRank.Name;
                Podium3Rank.Foreground = X.Hex(top3.HunterRank.Color);
                Podium3Score.Text = $"{top3.Score:N0} EXP";
            }

            // 3. Populate Ranks #4+
            RankedListPanel.Children.Clear();
            var remaining = entries.Skip(3).ToList();

            foreach (var item in remaining)
            {
                RankedListPanel.Children.Add(BuildLeaderboardRow(item));
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("LeaderboardPage.LoadLeaderboardAsync", ex);
        }
    }

    private UIElement BuildLeaderboardRow(LeaderboardEntry item)
    {
        var border = new Border
        {
            Background = item.IsCurrentUser
                ? (Application.Current.Resources.TryGetValue("AccentWashBrush", out var aw) ? (Brush)aw : null)
                : (Application.Current.Resources.TryGetValue("SurfaceBrush", out var sb) ? (Brush)sb : null),
            BorderBrush = item.IsCurrentUser
                ? (Application.Current.Resources.TryGetValue("AccentLightBrush", out var al) ? (Brush)al : null)
                : (Application.Current.Resources.TryGetValue("DividerBrush", out var db) ? (Brush)db : null),
            BorderThickness = new Thickness(item.IsCurrentUser ? 2 : 1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 10, 16, 10)
        };

        var grid = new Grid { ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 110 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });

        // Rank #
        var rankText = new TextBlock
        {
            Text = $"#{item.Rank}",
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            FontSize = 13,
            Foreground = item.IsCurrentUser
                ? (Application.Current.Resources.TryGetValue("AccentLightBrush", out var rA) ? (Brush)rA : null)
                : (Application.Current.Resources.TryGetValue("FgSubtleBrush", out var rS) ? (Brush)rS : null),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(rankText, 0);

        // Hunter Info (Initial + Name)
        var hunterStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        var initial = string.IsNullOrWhiteSpace(item.Username) ? "?" : item.Username[0].ToString().ToUpperInvariant();
        var avBorder = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(14),
            Background = Application.Current.Resources.TryGetValue("DarkChipBrush", out var dc) ? (Brush)dc : null,
            Child = new TextBlock
            {
                Text = initial,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        var nameText = new TextBlock
        {
            Text = item.Username,
            FontWeight = item.IsCurrentUser ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        hunterStack.Children.Add(avBorder);
        hunterStack.Children.Add(nameText);
        Grid.SetColumn(hunterStack, 1);

        // Tier Badge
        var tierBadge = new Border
        {
            Background = X.Hex(item.HunterRank.BgColor),
            BorderBrush = X.Hex(item.HunterRank.Color),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = item.HunterRank.Code,
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = X.Hex(item.HunterRank.Color)
            }
        };
        Grid.SetColumn(tierBadge, 2);

        // Streak
        var streakText = new TextBlock
        {
            Text = $"{item.StreakDays}d",
            FontSize = 13,
            Foreground = Application.Current.Resources.TryGetValue("FgSubtleBrush", out var stB) ? (Brush)stB : null,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(streakText, 3);

        // Chapters
        var chText = new TextBlock
        {
            Text = $"{item.ChaptersRead}",
            FontSize = 13,
            Foreground = Application.Current.Resources.TryGetValue("FgSubtleBrush", out var chB) ? (Brush)chB : null,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(chText, 4);

        // EXP Score
        var scoreText = new TextBlock
        {
            Text = $"{item.Score:N0} EXP",
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            FontSize = 13,
            Foreground = Application.Current.Resources.TryGetValue("FgBrush", out var scB) ? (Brush)scB : null,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(scoreText, 5);

        grid.Children.Add(rankText);
        grid.Children.Add(hunterStack);
        grid.Children.Add(tierBadge);
        grid.Children.Add(streakText);
        grid.Children.Add(chText);
        grid.Children.Add(scoreText);

        border.Content = grid;
        return border;
    }

    private static int CalculateStreakDays(IReadOnlyList<MangaDl.Core.Database.Entities.HistoryEntity> history)
    {
        if (history == null || history.Count == 0) return 0;

        var dates = history
            .Select(h => DateTimeOffset.FromUnixTimeMilliseconds(h.ReadAt).Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();

        if (dates.Count == 0) return 0;

        var today = DateTimeOffset.UtcNow.Date;
        if (dates[0] < today.AddDays(-1)) return 0; // Streak broken

        var streak = 0;
        var checkDate = dates[0];

        foreach (var d in dates)
        {
            if (d == checkDate)
            {
                streak++;
                checkDate = checkDate.AddDays(-1);
            }
            else
            {
                break;
            }
        }

        return streak;
    }
}
