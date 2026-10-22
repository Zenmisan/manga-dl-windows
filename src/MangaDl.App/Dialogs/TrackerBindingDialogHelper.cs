using MangaDl.Core;
using MangaDl.Core.Database.Entities;
using MangaDl.Core.Tracking;
using MangaDl.Helpers;
using MangaDl.Pages.Settings;
using MangaDl.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace MangaDl.Dialogs;

public static class TrackerBindingDialogHelper
{
    public static async Task ShowAsync(XamlRoot xamlRoot, Manga manga)
    {
        var anilistToken = CredentialStore.LoadTrackerToken("anilist_token");
        var malToken = CredentialStore.LoadTrackerToken("mal_token");

        var anilistConnected = AppServices.Settings.AnilistConnected && !string.IsNullOrEmpty(anilistToken);
        var malConnected = AppServices.Settings.MalConnected && !string.IsNullOrEmpty(malToken);

        var rootPanel = new StackPanel
        {
            Spacing = 16,
            Width = 460
        };

        var subtitle = new TextBlock
        {
            Text = $"Sync external reading progress, score, and status for \"{manga.Title}\".",
            Style = Application.Current.Resources.TryGetValue("BodySubtle", out var bStyle) ? (Style)bStyle : null,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 4)
        };
        rootPanel.Children.Add(subtitle);

        // ContentDialog instance
        var dialog = new ContentDialog
        {
            Title = "Tracker Synchronization",
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot
        };

        var contentContainer = new StackPanel { Spacing = 20 };
        var scroll = new ScrollViewer
        {
            Content = contentContainer,
            MaxHeight = 480
        };
        rootPanel.Children.Add(scroll);
        dialog.Content = rootPanel;

        async Task RefreshViewsAsync()
        {
            contentContainer.Children.Clear();
            var binds = await AppServices.Database.GetTrackerBindsAsync(manga.Source, manga.Id);

            // 1. AniList Section
            contentContainer.Children.Add(BuildTrackerCard(
                "AniList",
                "#1E3A5F",
                anilistConnected,
                anilistToken,
                binds.FirstOrDefault(b => b.Tracker.Equals("AniList", StringComparison.OrdinalIgnoreCase)),
                manga,
                dialog,
                RefreshViewsAsync));

            // 2. MyAnimeList Section
            contentContainer.Children.Add(BuildTrackerCard(
                "MyAnimeList",
                "#1F2C4F",
                malConnected,
                malToken,
                binds.FirstOrDefault(b => b.Tracker.Equals("MyAnimeList", StringComparison.OrdinalIgnoreCase)),
                manga,
                dialog,
                RefreshViewsAsync));
        }

        await RefreshViewsAsync();
        await dialog.ShowAsync();
    }

    private static FrameworkElement BuildTrackerCard(
        string trackerName,
        string headerHex,
        bool isConnected,
        string? token,
        TrackerBindEntity? bind,
        Manga manga,
        ContentDialog dialog,
        Func<Task> onRefresh)
    {
        var card = new Border
        {
            Background = Application.Current.Resources.TryGetValue("SurfaceBrush", out var sBrush) ? (Brush)sBrush : new SolidColorBrush(ColorHelper.FromArgb(20, 255, 255, 255)),
            BorderBrush = Application.Current.Resources.TryGetValue("DividerBrush", out var dBrush) ? (Brush)dBrush : new SolidColorBrush(ColorHelper.FromArgb(20, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16)
        };

        var stack = new StackPanel { Spacing = 12 };
        card.Content = stack;

        // Header Row
        var headerRow = new Grid();
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        var dot = new Border
        {
            Width = 10,
            Height = 10,
            CornerRadius = new CornerRadius(5),
            Background = isConnected ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Gray),
            VerticalAlignment = VerticalAlignment.Center
        };
        var titleText = new TextBlock
        {
            Text = trackerName,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            FontSize = 15,
            VerticalAlignment = VerticalAlignment.Center
        };
        titleStack.Children.Add(dot);
        titleStack.Children.Add(titleText);
        Grid.SetColumn(titleStack, 0);
        headerRow.Children.Add(titleStack);

        var statusBadge = new TextBlock
        {
            Text = !isConnected ? "Not Connected" : (bind != null ? "Linked" : "Not Linked"),
            FontSize = 12,
            Foreground = !isConnected
                ? new SolidColorBrush(Colors.Gray)
                : (bind != null ? new SolidColorBrush(Colors.LightGreen) : new SolidColorBrush(Colors.Orange)),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(statusBadge, 1);
        headerRow.Children.Add(statusBadge);
        stack.Children.Add(headerRow);

        if (!isConnected)
        {
            var notConnStack = new StackPanel { Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };
            var hint = new TextBlock
            {
                Text = $"Connect your {trackerName} account in Settings to enable tracking.",
                FontSize = 12,
                Foreground = Application.Current.Resources.TryGetValue("FgSubtleBrush", out var subBrush) ? (Brush)subBrush : null,
                TextWrapping = TextWrapping.Wrap
            };
            var settingsBtn = new Button
            {
                Content = $"Open {trackerName} Settings",
                Style = Application.Current.Resources.TryGetValue("GhostButton", out var gBtn) ? (Style)gBtn : null,
                Height = 32,
                FontSize = 12
            };
            settingsBtn.Click += (_, _) =>
            {
                dialog.Hide();
                Nav.Go(typeof(TrackersSettingsPage));
            };
            notConnStack.Children.Add(hint);
            notConnStack.Children.Add(settingsBtn);
            stack.Children.Add(notConnStack);
            return card;
        }

        if (bind != null)
        {
            // Already bound view
            var boundStack = new StackPanel { Spacing = 10 };

            var mediaTitle = new TextBlock
            {
                Text = bind.RemoteTitle ?? $"ID: #{bind.RemoteId}",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 14,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            boundStack.Children.Add(mediaTitle);

            var metaText = new TextBlock
            {
                Text = $"Remote ID: #{bind.RemoteId} · Last Synced Ch: {bind.LastChapterRead}",
                FontSize = 12,
                Foreground = Application.Current.Resources.TryGetValue("FgSubtleBrush", out var sSub) ? (Brush)sSub : null
            };
            boundStack.Children.Add(metaText);

            // Controls row (Status & Score)
            var controlsRow = new Grid { ColumnSpacing = 12, Margin = new Thickness(0, 4, 0, 0) };
            controlsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            controlsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var statusCombo = new ComboBox
            {
                Header = "Status",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                FontSize = 12
            };
            statusCombo.Items.Add("Reading");
            statusCombo.Items.Add("Completed");
            statusCombo.Items.Add("On Hold");
            statusCombo.Items.Add("Dropped");
            statusCombo.Items.Add("Plan to Read");

            var currentStatus = bind.Status?.ToUpperInvariant() switch
            {
                "COMPLETED" => "Completed",
                "ON_HOLD" or "ONHOLD" => "On Hold",
                "DROPPED" => "Dropped",
                "PLAN_TO_READ" or "PLANTOREAD" => "Plan to Read",
                _ => "Reading"
            };
            statusCombo.SelectedItem = currentStatus;

            var scoreBox = new NumberBox
            {
                Header = "Score (0 - 10)",
                Value = bind.Score,
                Minimum = 0,
                Maximum = 10,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
                FontSize = 12
            };

            Grid.SetColumn(statusCombo, 0);
            Grid.SetColumn(scoreBox, 1);
            controlsRow.Children.Add(statusCombo);
            controlsRow.Children.Add(scoreBox);
            boundStack.Children.Add(controlsRow);

            // Action buttons row (Save/Sync, Unlink)
            var actionRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };

            var syncBtn = new Button
            {
                Content = "Sync Now",
                Style = Application.Current.Resources.TryGetValue("PrimaryButton", out var pStyle) ? (Style)pStyle : null,
                Height = 32,
                FontSize = 12
            };
            syncBtn.Click += async (_, _) =>
            {
                syncBtn.IsEnabled = false;
                try
                {
                    var isCompleted = (statusCombo.SelectedItem as string) == "Completed";
                    bind.Status = (statusCombo.SelectedItem as string)?.ToUpperInvariant();
                    bind.Score = scoreBox.Value;
                    await AppServices.Database.SaveTrackerBindAsync(bind);

                    if (trackerName == "AniList" && !string.IsNullOrEmpty(token))
                    {
                        var ok = await AppServices.Trackers.UpdateAnilistProgressAsync(token, bind.RemoteId, (int)bind.LastChapterRead, isCompleted);
                        Nav.Toast(ok ? "AniList progress synced" : "Failed to sync to AniList");
                    }
                    else if (trackerName == "MyAnimeList" && !string.IsNullOrEmpty(token))
                    {
                        var ok = await AppServices.Trackers.UpdateMalProgressAsync(token, bind.RemoteId, (int)bind.LastChapterRead, isCompleted);
                        Nav.Toast(ok ? "MyAnimeList progress synced" : "Failed to sync to MyAnimeList");
                    }
                }
                catch (Exception ex)
                {
                    AppLog.Warn($"SyncTracker: {trackerName}", ex);
                    Nav.Toast($"Failed to sync {trackerName}");
                }
                finally
                {
                    syncBtn.IsEnabled = true;
                }
            };

            var unlinkBtn = new Button
            {
                Content = "Unlink",
                Style = Application.Current.Resources.TryGetValue("GhostButton", out var gStyle) ? (Style)gStyle : null,
                Height = 32,
                FontSize = 12
            };
            unlinkBtn.Click += async (_, _) =>
            {
                await AppServices.Database.DeleteTrackerBindAsync(manga.Source, manga.Id, trackerName);
                Nav.Toast($"Unlinked from {trackerName}");
                await onRefresh();
            };

            actionRow.Children.Add(syncBtn);
            actionRow.Children.Add(unlinkBtn);
            boundStack.Children.Add(actionRow);

            stack.Children.Add(boundStack);
            return card;
        }

        // Unbound view: Search & Bind UI
        var searchSection = new StackPanel { Spacing = 8 };

        var searchGrid = new Grid { ColumnSpacing = 8 };
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var searchBox = new TextBox
        {
            Text = manga.Title,
            PlaceholderText = $"Search {trackerName} for manga title...",
            FontSize = 12
        };
        var searchBtn = new Button
        {
            Content = "Search",
            Style = Application.Current.Resources.TryGetValue("GhostButton", out var sBtn) ? (Style)sBtn : null,
            Height = 32,
            FontSize = 12
        };

        Grid.SetColumn(searchBox, 0);
        Grid.SetColumn(searchBtn, 1);
        searchGrid.Children.Add(searchBox);
        searchGrid.Children.Add(searchBtn);
        searchSection.Children.Add(searchGrid);

        var resultsStack = new StackPanel { Spacing = 6 };
        var resultsScroll = new ScrollViewer
        {
            Content = resultsStack,
            MaxHeight = 180,
            Visibility = Visibility.Collapsed
        };
        searchSection.Children.Add(resultsScroll);

        searchBtn.Click += async (_, _) =>
        {
            var query = searchBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(query)) return;

            searchBtn.IsEnabled = false;
            resultsStack.Children.Clear();
            resultsStack.Children.Add(new TextBlock
            {
                Text = "Searching...",
                FontSize = 12,
                Foreground = Application.Current.Resources.TryGetValue("FgSubtleBrush", out var sb) ? (Brush)sb : null,
                Margin = new Thickness(4)
            });
            resultsScroll.Visibility = Visibility.Visible;

            try
            {
                List<TrackerSearchResult> results;
                if (trackerName == "AniList")
                {
                    results = await AppServices.Trackers.SearchAnilistAsync(query, token);
                }
                else
                {
                    results = await AppServices.Trackers.SearchMalAsync(query, token!);
                }

                resultsStack.Children.Clear();
                if (results.Count == 0)
                {
                    resultsStack.Children.Add(new TextBlock
                    {
                        Text = "No results found.",
                        FontSize = 12,
                        Foreground = Application.Current.Resources.TryGetValue("FgSubtleBrush", out var sb2) ? (Brush)sb2 : null,
                        Margin = new Thickness(4)
                    });
                    return;
                }

                foreach (var r in results)
                {
                    var rRow = new Grid
                    {
                        Padding = new Thickness(8, 4, 8, 4),
                        ColumnSpacing = 8
                    };
                    rRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    rRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    var rTitle = new TextBlock
                    {
                        Text = r.Title,
                        FontSize = 12,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };
                    var rMeta = new TextBlock
                    {
                        Text = $"ID: #{r.Id} · Chapters: {(r.TotalChapters > 0 ? r.TotalChapters.ToString() : "Ongoing")}",
                        FontSize = 11,
                        Foreground = Application.Current.Resources.TryGetValue("FgSubtleBrush", out var s3) ? (Brush)s3 : null
                    };
                    infoStack.Children.Add(rTitle);
                    infoStack.Children.Add(rMeta);
                    Grid.SetColumn(infoStack, 0);

                    var bindBtn = new Button
                    {
                        Content = "Link",
                        Style = Application.Current.Resources.TryGetValue("PrimaryButton", out var pb) ? (Style)pb : null,
                        Height = 28,
                        FontSize = 11,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    bindBtn.Click += async (_, _) =>
                    {
                        bindBtn.IsEnabled = false;
                        var newBind = new TrackerBindEntity
                        {
                            Provider = manga.Source,
                            MangaId = manga.Id,
                            Tracker = trackerName,
                            RemoteId = r.Id,
                            RemoteTitle = r.Title,
                            Status = "CURRENT",
                            Score = 0,
                            LastChapterRead = 0
                        };
                        await AppServices.Database.SaveTrackerBindAsync(newBind);
                        Nav.Toast($"Linked \"{manga.Title}\" to {trackerName}");
                        await onRefresh();
                    };
                    Grid.SetColumn(bindBtn, 1);

                    rRow.Children.Add(infoStack);
                    rRow.Children.Add(bindBtn);
                    resultsStack.Children.Add(rRow);
                }
            }
            catch (Exception ex)
            {
                AppLog.Warn($"SearchTracker: {trackerName}", ex);
                resultsStack.Children.Clear();
                resultsStack.Children.Add(new TextBlock
                {
                    Text = "Search failed. Check your network connection.",
                    FontSize = 12,
                    Foreground = Application.Current.Resources.TryGetValue("FgSubtleBrush", out var s4) ? (Brush)s4 : null
                });
            }
            finally
            {
                searchBtn.IsEnabled = true;
            }
        };

        stack.Children.Add(searchSection);
        return card;
    }
}
