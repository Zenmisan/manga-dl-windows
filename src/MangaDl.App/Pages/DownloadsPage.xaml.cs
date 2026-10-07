using System.Collections.ObjectModel;
using MangaDl.Core;
using MangaDl.Core.Database.Entities;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages;

public sealed partial class DownloadsPage : Page
{
    public ObservableCollection<DownloadItem> Items { get; } = new(Sample.Downloads);

    private bool _paused;

    public DownloadsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadDownloadsAsync();

        AppServices.Downloads.DownloadProgressChanged += OnDownloadUpdated;
        AppServices.Downloads.DownloadCompleted += OnDownloadUpdated;
    }

    private readonly List<DownloadItem> _allDownloads = [];
    private string _currentTab = "Queue";

    private async Task LoadDownloadsAsync()
    {
        try
        {
            var dir = AppServices.Settings.DownloadDirectory;
            FolderText.Text = string.IsNullOrEmpty(dir) ? "~/manga-library" : dir;

            var records = await AppServices.Database.GetDownloadsAsync();
            if (records.Count > 0)
            {
                _allDownloads.Clear();
                foreach (var r in records)
                {
                    var manga = new Manga(r.MangaId, r.MangaTitle, "#2E2412", 0, false, false, r.Provider);
                    var state = r.Status switch
                    {
                        "downloading" => DownloadState.Downloading,
                        "paused" => DownloadState.Paused,
                        "failed" => DownloadState.Failed,
                        "completed" => DownloadState.Done,
                        _ => DownloadState.Queued
                    };

                    var progress = r.TotalPages > 0 ? (double)r.Progress / 100.0 : 0.0;
                    var statusText = state == DownloadState.Downloading
                        ? $"Downloading · {r.Progress}% ({r.TotalPages} pages)"
                        : state.ToString();

                    _allDownloads.Add(new DownloadItem(manga, r.ChapterTitle ?? $"Ch. {r.ChapterNumber}", statusText, progress, state));
                }
            }
            else
            {
                _allDownloads.Clear();
                _allDownloads.AddRange(Sample.Downloads);
            }

            UpdateTabCounters();
            ApplyTabFilter();
        }
        catch (Exception ex)
        {
            AppLog.Warn("DownloadsPage.LoadDownloadsAsync", ex);
            Nav.Toast("Couldn't load downloads");
        }
    }

    private void UpdateTabCounters()
    {
        var queued = _allDownloads.Count(d => d.State is DownloadState.Queued or DownloadState.Downloading or DownloadState.Paused);
        var finished = _allDownloads.Count(d => d.State == DownloadState.Done);
        var failed = _allDownloads.Count(d => d.State == DownloadState.Failed);

        if (TabQueue != null) TabQueue.Content = $"Queue · {queued}";
        if (TabFinished != null) TabFinished.Content = $"Finished · {finished}";
        if (TabFailed != null) TabFailed.Content = $"Failed · {failed}";
    }

    private void ApplyTabFilter()
    {
        var filtered = _currentTab switch
        {
            "Finished" => _allDownloads.Where(d => d.State == DownloadState.Done),
            "Failed" => _allDownloads.Where(d => d.State == DownloadState.Failed),
            _ => _allDownloads.Where(d => d.State != DownloadState.Done)
        };

        Items.Clear();
        foreach (var item in filtered) Items.Add(item);
    }

    private void OnTabChecked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Content is string text)
        {
            _currentTab = text.Split('·')[0].Trim();
            ApplyTabFilter();
        }
    }

    private void OnItemAction(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is DownloadItem item)
        {
            Nav.Toast($"Toggled {item.Manga.Title} {item.Chapter}");
        }
    }

    private void OnItemCancel(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is DownloadItem item)
        {
            _allDownloads.Remove(item);
            Items.Remove(item);
            UpdateTabCounters();
            Nav.Toast($"Cancelled {item.Chapter}");
        }
    }

    private async void OnClearFinished(object sender, RoutedEventArgs e)
    {
        try
        {
            await AppServices.Database.ClearCompletedDownloadsAsync();
            _allDownloads.RemoveAll(d => d.State == DownloadState.Done);
            UpdateTabCounters();
            ApplyTabFilter();
            Nav.Toast("Finished downloads cleared");
        }
        catch (Exception ex)
        {
            AppLog.Warn("DownloadsPage.OnClearFinished", ex);
            Nav.Toast("Couldn't clear finished downloads");
        }
    }

    private void OnChangeFolder(object sender, RoutedEventArgs e) =>
        Nav.Toast("Select a new download directory in Settings > System");

    private void OnDownloadUpdated(DownloadEntity entity)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var existing = _allDownloads.FirstOrDefault(i => i.Manga.Id == entity.MangaId && i.Chapter == (entity.ChapterTitle ?? $"Ch. {entity.ChapterNumber}"));
            var state = entity.Status switch
            {
                "downloading" => DownloadState.Downloading,
                "paused" => DownloadState.Paused,
                "failed" => DownloadState.Failed,
                "completed" => DownloadState.Done,
                _ => DownloadState.Queued
            };
            var progress = entity.TotalPages > 0 ? (double)entity.Progress / 100.0 : 0.0;

            if (existing != null)
            {
                var idx = _allDownloads.IndexOf(existing);
                _allDownloads[idx] = new DownloadItem(existing.Manga, existing.Chapter, $"{entity.Status} · {entity.Progress}%", progress, state);
            }
            UpdateTabCounters();
            ApplyTabFilter();
        });
    }

    private void OnPauseAll(object sender, RoutedEventArgs e)
    {
        _paused = !_paused;
        if (_paused)
        {
            AppServices.Downloads.Pause();
            PauseAll.Content = "Resume All";
            Nav.Toast("Download queue paused");
        }
        else
        {
            AppServices.Downloads.Resume();
            PauseAll.Content = "Pause All";
            Nav.Toast("Download queue resumed");
        }
    }
}
