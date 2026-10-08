using System.Collections.ObjectModel;
using System.Globalization;
using MangaDl.Core;
using MangaDl.Core.Updates;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages;

public sealed partial class UpdatesPage : Page
{
    public ObservableCollection<UpdateGroup> Groups { get; } = new();

    public UpdatesPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadUpdatesAsync();
    }

    private async Task LoadUpdatesAsync()
    {
        UpdateLastCheckedDisplay();

        try
        {
            var newChapters = await AppServices.Database.GetNewChaptersAsync();
            if (newChapters.Count > 0)
            {
                var grouped = UpdateCheckerService.GroupUpdates(newChapters);
                Groups.Clear();
                foreach (var g in grouped) Groups.Add(g);
                EmptyStateText.Visibility = Visibility.Collapsed;
            }
            else
            {
                Groups.Clear();
                if (AppServices.Settings.LastUpdateCheck == 0)
                {
                    // Fall back to sample data on pristine install before first check
                    foreach (var g in Sample.Updates) Groups.Add(g);
                    EmptyStateText.Visibility = Visibility.Collapsed;
                }
                else
                {
                    EmptyStateText.Visibility = Visibility.Visible;
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("UpdatesPage.LoadUpdatesAsync", ex);
        }
    }

    private void UpdateLastCheckedDisplay()
    {
        var last = AppServices.Settings.LastUpdateCheck;
        if (last <= 0)
        {
            LastCheckedText.Text = "Not checked yet · background sync every 30 min";
            return;
        }

        var diffMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - last;
        string relative;
        if (diffMs < 60_000) relative = "just now";
        else if (diffMs < 3_600_000) relative = $"{diffMs / 60_000}m ago";
        else if (diffMs < 86_400_000) relative = $"{diffMs / 3_600_000}h ago";
        else relative = DateTimeOffset.FromUnixTimeMilliseconds(last).ToLocalTime().ToString("MMM d, HH:mm", CultureInfo.InvariantCulture);

        LastCheckedText.Text = $"Last checked {relative} · background sync every 30 min";
    }

    private void OnOpenManga(object sender, RoutedEventArgs e) =>
        Nav.Go(typeof(MangaDetailPage), (((FrameworkElement)sender).DataContext as UpdateItem)?.Manga);

    private void OnRead(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is UpdateItem item)
        {
            var isNovel = AppServices.Extensions.FindExtension(item.Manga.Source)?.Type == "novel";
            var targetType = isNovel ? typeof(NovelReaderPage) : typeof(ReaderPage);

            if (!string.IsNullOrEmpty(item.ChapterId))
            {
                var args = new ReaderNavigationArgs(item.Manga, item.ChapterId, item.Chapter, item.ChapterNumber);
                Nav.Go(targetType, args);
            }
            else
            {
                Nav.Go(targetType, item.Manga);
            }
        }
        else
        {
            Nav.Go(typeof(ReaderPage));
        }
    }

    private async void OnDownloadChapter(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is UpdateItem item)
        {
            try
            {
                await AppServices.Downloads.EnqueueChapterAsync(
                    item.Manga.Source,
                    item.Manga.Id,
                    item.Manga.Title,
                    item.ChapterId ?? "1",
                    item.Chapter,
                    item.ChapterNumber);

                Nav.Toast($"Queued download for {item.Manga.Title} {item.Chapter}");
            }
            catch (Exception ex)
            {
                AppLog.Warn("UpdatesPage.OnDownloadChapter", ex);
                Nav.Toast("Couldn't queue download");
            }
        }
    }

    private async void OnDownloadAllNew(object sender, RoutedEventArgs e)
    {
        var count = 0;
        foreach (var group in Groups)
        {
            foreach (var item in group.Items)
            {
                try
                {
                    await AppServices.Downloads.EnqueueChapterAsync(
                        item.Manga.Source,
                        item.Manga.Id,
                        item.Manga.Title,
                        item.ChapterId ?? "1",
                        item.Chapter,
                        item.ChapterNumber);
                    count++;
                }
                catch (Exception ex)
                {
                    AppLog.Warn("UpdatesPage.OnDownloadAllNew", ex);
                }
            }
        }

        if (count > 0)
        {
            Nav.Toast($"Queued {count} new chapters for download");
        }
        else
        {
            Nav.Toast("No new chapters to download");
        }
    }

    private async void OnCheck(object sender, RoutedEventArgs e)
    {
        CheckButton.IsEnabled = false;
        CheckButton.Content = "Checking...";
        Nav.Toast("Checking library for new chapters...");

        try
        {
            var progress = new Progress<string>(msg =>
            {
                LastCheckedText.Text = msg;
            });

            var result = await AppServices.UpdateChecker.CheckUpdatesAsync(
                autoDownload: AppServices.Settings.AutoDownloadNew,
                progress: progress);

            AppServices.Settings.LastUpdateCheck = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            AppServices.Settings.Save();

            await LoadUpdatesAsync();

            if (result.NewChaptersFound > 0)
            {
                Nav.Toast($"Found {result.NewChaptersFound} new chapters across {result.TotalChecked} series");
            }
            else
            {
                Nav.Toast("Library is up to date");
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("UpdatesPage.OnCheck", ex);
            Nav.Toast("Couldn't check for updates");
        }
        finally
        {
            CheckButton.IsEnabled = true;
            CheckButton.Content = "Check Now";
            UpdateLastCheckedDisplay();
        }
    }
}
