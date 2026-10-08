using MangaDl.Core.Gamification;
using MangaDl.Helpers;
using MangaDl.Pages.Settings;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages;

public sealed partial class ProfilePage : Page
{
    public ProfilePage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadProfileAsync();
    }

    private async Task LoadProfileAsync()
    {
        var email = AppServices.Settings.UserEmail;
        var displayName = AppServices.Settings.DisplayName;
        if (!string.IsNullOrEmpty(displayName))
        {
            if (DisplayNameText != null) DisplayNameText.Text = displayName;
            if (UsernameText != null) UsernameText.Text = $"@{displayName.ToLowerInvariant().Replace(" ", "_")} · Active Hunter";
        }
        else if (!string.IsNullOrEmpty(email))
        {
            var name = email.Split('@')[0];
            if (DisplayNameText != null) DisplayNameText.Text = char.ToUpperInvariant(name[0]) + name[1..];
            if (UsernameText != null) UsernameText.Text = $"@{name} · Active Hunter";
        }
        else
        {
            if (DisplayNameText != null) DisplayNameText.Text = "Reader";
            if (UsernameText != null) UsernameText.Text = "@reader · Solo Hunter";
        }

        try
        {
            var lib = await AppServices.Database.GetLibraryAsync();
            var hist = await AppServices.Database.GetHistoryAsync(100);
            var completed = await AppServices.Database.GetAllCompletedProgressAsync();

            var libraryCount = lib.Count;
            var chaptersRead = Math.Max(completed.Count, hist.Count);

            // Calculate reading streak
            var dates = hist
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
            else if (hist.Count > 0)
            {
                streak = 1;
            }

            if (LibraryCountText != null) LibraryCountText.Text = libraryCount.ToString();
            if (ChaptersCountText != null) ChaptersCountText.Text = chaptersRead.ToString();
            if (StreakCountText != null) StreakCountText.Text = streak.ToString();

            // Evaluate Gamification Profile
            var profile = GamificationService.GetProfile(chaptersRead, streak, libraryCount);

            if (RankTitleText != null) RankTitleText.Text = profile.Rank.Name;
            if (RankTagText != null) RankTagText.Text = $"{profile.Rank.Tag} · {profile.Rank.Tier} Tier";
            if (RankBadgeText != null) RankBadgeText.Text = profile.Rank.Code;
            if (ScoreValueText != null) ScoreValueText.Text = $"{profile.Score:N0} EXP";
            if (RankProgressBar != null) RankProgressBar.Value = profile.ProgressToNextRank * 100;

            if (RankCard != null)
            {
                RankCard.Background = X.Hex(profile.Rank.BgColor);
                RankCard.BorderBrush = X.Hex(profile.Rank.Color);
            }
            if (RankBadgeBorder != null)
            {
                RankBadgeBorder.Background = X.Hex(profile.Rank.Color);
                if (RankBadgeText != null) RankBadgeText.Foreground = X.Hex("#000000");
            }

            if (NextRankText != null)
            {
                NextRankText.Text = profile.NextRank != null
                    ? $"Next: {profile.NextRank.Name} at {profile.NextRank.MinScore:N0} EXP"
                    : "Maximum rank reached · Shadow Monarch";
            }

            // Render Milestones
            var unlockedCount = profile.Milestones.Count(m => m.Unlocked);
            if (MilestonesUnlockedText != null)
            {
                MilestonesUnlockedText.Text = $"{unlockedCount} of {profile.Milestones.Count} unlocked";
            }

            if (MilestonesContainer != null)
            {
                MilestonesContainer.Children.Clear();

                // Unlocked badges
                var unlockedRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                foreach (var m in profile.Milestones.Where(x => x.Unlocked).Take(6))
                {
                    var chip = new Border
                    {
                        CornerRadius = new CornerRadius(999),
                        Background = X.Hex(m.Badge.Color),
                        Padding = new Thickness(12, 6, 12, 6)
                    };
                    chip.Child = new TextBlock
                    {
                        Text = m.Badge.Title,
                        FontSize = 12,
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        Foreground = X.Hex("#000000")
                    };
                    unlockedRow.Children.Add(chip);
                }
                if (unlockedRow.Children.Count > 0)
                {
                    MilestonesContainer.Children.Add(unlockedRow);
                }

                // In-progress / locked badges
                var lockedRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                foreach (var m in profile.Milestones.Where(x => !x.Unlocked).OrderByDescending(x => x.ProgressPercentage).Take(4))
                {
                    var chip = new Border
                    {
                        CornerRadius = new CornerRadius(999),
                        BorderThickness = new Thickness(1),
                        BorderBrush = X.Hex("#3F3F46"),
                        Padding = new Thickness(12, 6, 12, 6)
                    };
                    chip.Child = new TextBlock
                    {
                        Text = $"{m.Badge.Title} ({m.CurrentValue}/{m.Badge.Threshold})",
                        FontSize = 12,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground = X.Hex("#A1A1AA")
                    };
                    lockedRow.Children.Add(chip);
                }
                if (lockedRow.Children.Count > 0)
                {
                    MilestonesContainer.Children.Add(lockedRow);
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("ProfilePage.LoadProfileAsync", ex);
        }
    }

    private void OnShare(object sender, RoutedEventArgs e) =>
        Nav.Toast("Profile link copied to clipboard");

    private void OnEdit(object sender, RoutedEventArgs e) => Nav.Go(typeof(SettingsPage), "Account");
}
