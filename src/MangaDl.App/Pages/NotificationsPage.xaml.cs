using System.Collections.ObjectModel;
using MangaDl.Core;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages;

public sealed partial class NotificationsPage : Page
{
    private readonly List<Notice> _allNotices = [.. Sample.Notices];
    public ObservableCollection<Notice> Items { get; } = new(Sample.Notices);

    public NotificationsPage() => InitializeComponent();

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
                Nav.Toast($"Opened: {AppServices.Settings.DownloadFolder}");
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
        var tag = (string)((FrameworkElement)sender).Tag;
        Items.Clear();
        var filtered = tag switch
        {
            "Unread" => _allNotices.Where(n => n.Unread),
            "Chapters" => _allNotices.Where(n => n.Title.Contains("chapter", StringComparison.OrdinalIgnoreCase) || n.Body.Contains("Ch.", StringComparison.OrdinalIgnoreCase)),
            "Downloads" => _allNotices.Where(n => n.Title.Contains("download", StringComparison.OrdinalIgnoreCase) || n.Body.Contains("download", StringComparison.OrdinalIgnoreCase)),
            "App" => _allNotices.Where(n => n.Title.Contains("update", StringComparison.OrdinalIgnoreCase) || n.Title.Contains("extension", StringComparison.OrdinalIgnoreCase) || n.Title.Contains("synced", StringComparison.OrdinalIgnoreCase)),
            _ => _allNotices,
        };
        foreach (var item in filtered) Items.Add(item);
    }
}
