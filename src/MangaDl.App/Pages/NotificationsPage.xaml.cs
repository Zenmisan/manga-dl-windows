using System.Collections.ObjectModel;
using MangaDl.Core;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages;

public sealed partial class NotificationsPage : Page
{
    private readonly List<Notice> _allNotices = [];
    public ObservableCollection<Notice> Items { get; } = [];

    public NotificationsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadNoticesAsync();
    }

    private async Task LoadNoticesAsync()
    {
        try
        {
            _allNotices.Clear();
            Items.Clear();

            // 1. App notice
            _allNotices.Add(new Notice(
                "manga-dl v1.0.0",
                "Desktop client is running with native WinUI3 runtime.",
                "Today",
                false,
                NoticeKind.Info,
                "Check",
                "Install"));

            // 2. Real new chapters discovered
            var newChapters = await AppServices.Database.GetNewChaptersAsync(15);
            foreach (var ch in newChapters)
            {
                var manga = await AppServices.Database.GetLibraryItemAsync(ch.Provider, ch.MangaId);
                var title = manga?.Title ?? ch.MangaId;
                _allNotices.Add(new Notice(
                    "New Chapter Available",
                    $"{ch.Title} is out for {title}",
                    "Recent",
                    true,
                    NoticeKind.Success,
                    "Book",
                    "View"));
            }

            // 3. Completed downloads
            var downloads = await AppServices.Database.GetCompletedDownloadsAsync();
            foreach (var dl in downloads.Take(10))
            {
                _allNotices.Add(new Notice(
                    "Download Complete",
                    $"{dl.ChapterTitle}",
                    "Downloaded",
                    false,
                    NoticeKind.Success,
                    "Download",
                    "Open Folder"));
            }

            foreach (var n in _allNotices)
            {
                Items.Add(n);
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("NotificationsPage.LoadNoticesAsync", ex);
        }
    }

    private void OnMarkAllRead(object sender, RoutedEventArgs e)
    {
        for (var i = 0; i < Items.Count; i++)
        {
            if (Items[i].Unread)
            {
                Items[i] = Items[i] with { Unread = false };
            }
        }
        for (var i = 0; i < _allNotices.Count; i++)
        {
            if (_allNotices[i].Unread)
            {
                _allNotices[i] = _allNotices[i] with { Unread = false };
            }
        }
        Nav.Toast("All notifications marked as read");
    }

    private void OnNoticeAction(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is Notice notice)
        {
            if (notice.Action == "Install")
            {
                Nav.ShowUpdateDialog();
            }
            else if (notice.Action == "Open Folder")
            {
                Nav.Toast($"Opened: {AppServices.Settings.DownloadPath}");
            }
            else if (notice.Action == "View")
            {
                Nav.Go(typeof(UpdatesPage));
            }
            else if (notice.Action == "Retry")
            {
                Nav.Toast($"Retrying: {notice.Title}");
            }
            else
            {
                Nav.Toast($"Action: {notice.Action}");
            }
        }
    }

    private void OnFilterTab(object sender, RoutedEventArgs e)
    {
        var tag = (sender as FrameworkElement)?.Tag as string ?? "All";
        Items.Clear();

        IEnumerable<Notice> filtered = tag switch
        {
            "Unread" => _allNotices.Where(n => n.Unread),
            "Chapters" => _allNotices.Where(n => n.Action == "View"),
            "Downloads" => _allNotices.Where(n => n.Action == "Open Folder"),
            "App" => _allNotices.Where(n => n.Action == "Install" || n.Action == null),
            _ => _allNotices
        };

        foreach (var notice in filtered)
        {
            Items.Add(notice);
        }
    }
}
