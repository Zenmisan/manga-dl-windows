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

    private async Task LoadDownloadsAsync()
    {
        try
        {
            var records = await AppServices.Database.GetDownloadsAsync();
            if (records.Count > 0)
            {
                Items.Clear();
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

                    Items.Add(new DownloadItem(manga, r.ChapterTitle ?? $"Ch. {r.ChapterNumber}", statusText, progress, state));
                }
            }
        }
        catch { }
    }

    private void OnDownloadUpdated(DownloadEntity entity)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var existing = Items.FirstOrDefault(i => i.Manga.Id == entity.MangaId && i.Chapter == (entity.ChapterTitle ?? $"Ch. {entity.ChapterNumber}"));
            if (existing != null)
            {
                var idx = Items.IndexOf(existing);
                var state = entity.Status switch
                {
                    "downloading" => DownloadState.Downloading,
                    "paused" => DownloadState.Paused,
                    "failed" => DownloadState.Failed,
                    "completed" => DownloadState.Done,
                    _ => DownloadState.Queued
                };
                var progress = entity.TotalPages > 0 ? (double)entity.Progress / 100.0 : 0.0;
                Items[idx] = new DownloadItem(existing.Manga, existing.Chapter, $"{entity.Status} · {entity.Progress}%", progress, state);
            }
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
