using System.Collections.ObjectModel;
using MangaDl.Core;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages;

public sealed partial class HistoryPage : Page
{
    public ObservableCollection<HistoryItem> Items { get; } = new(Sample.History);
    private readonly List<HistoryItem> _allHistory = [];
    private string _searchFilter = string.Empty;

    public HistoryPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadHistoryAsync();
    }

    private async Task LoadHistoryAsync()
    {
        try
        {
            var records = await AppServices.Database.GetHistoryAsync(50);
            PopulateHistory(records);

            // Pull cloud history in background if user is authenticated
            if (!string.IsNullOrEmpty(AppServices.Settings.UserId))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var (token, _) = CredentialStore.Load();
                        if (!string.IsNullOrEmpty(token))
                        {
                            var count = await AppServices.Sync.PullReadingProgressAndHistoryAsync(AppServices.Settings.UserId, token);
                            if (count > 0)
                            {
                                var refreshed = await AppServices.Database.GetHistoryAsync(50);
                                DispatcherQueue?.TryEnqueue(() => PopulateHistory(refreshed));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLog.Warn("HistoryPage.CloudPull", ex);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("HistoryPage.LoadHistoryAsync", ex);
            Nav.Toast("Couldn't load history");
        }
    }

    private void PopulateHistory(List<HistoryEntity> records)
    {
        _allHistory.Clear();
        if (records.Count > 0)
        {
            foreach (var r in records)
            {
                var manga = new Manga(r.MangaId, r.MangaTitle, r.CoverUrl ?? "#1A2433", 0, false, false, r.Provider);
                var dt = DateTimeOffset.FromUnixTimeMilliseconds(r.ReadAt);
                var when = dt.Date == DateTimeOffset.UtcNow.Date ? $"Today · {dt:HH:mm}" : dt.ToString("MMM d");

                _allHistory.Add(new HistoryItem(manga, r.ChapterTitle ?? r.ChapterId, when));
            }
        }
        else
        {
            _allHistory.AddRange(Sample.History);
        }
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var filtered = _allHistory.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(_searchFilter))
        {
            filtered = filtered.Where(h => h.Manga.Title.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase));
        }

        Items.Clear();
        foreach (var h in filtered) Items.Add(h);
    }

    private void OnSearchSubmitted(object? sender, string query)
    {
        _searchFilter = query ?? string.Empty;
        ApplyFilters();
    }

    private void OnRangeChecked(object sender, RoutedEventArgs e)
    {
        // Filter by range
        ApplyFilters();
    }

    private async void OnClearHistory(object sender, RoutedEventArgs e)
    {
        try
        {
            await AppServices.Database.ClearHistoryAsync();
            _allHistory.Clear();
            Items.Clear();
            Nav.Toast("Reading history cleared");
        }
        catch (Exception ex)
        {
            AppLog.Warn("HistoryPage.OnClearHistory", ex);
            Nav.Toast("Couldn't clear history");
        }
    }

    private void OnDeleteItem(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is HistoryItem item)
        {
            _allHistory.Remove(item);
            Items.Remove(item);
            Nav.Toast($"Removed {item.Manga.Title} from history");
        }
    }

    private void OnResume(object sender, RoutedEventArgs e)
    {
        var item = ((FrameworkElement)sender).DataContext as HistoryItem;
        Nav.Go(typeof(ReaderPage), item?.Manga);
    }
}
