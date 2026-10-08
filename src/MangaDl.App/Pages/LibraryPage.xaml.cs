using System.Collections.ObjectModel;
using MangaDl.Core;
using MangaDl.Core.Database.Entities;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages;

public sealed partial class LibraryPage : Page
{
    public ObservableCollection<Manga> Items { get; } = new(Sample.Library);
    public ObservableCollection<ContinueItem> Continue { get; } = new(Sample.ContinueReading);

    private readonly List<Manga> _allManga = [];
    private int _sortMode; // 0 = Last read, 1 = Title A-Z, 2 = Unread
    private string _currentCategory = "All";
    private string _searchFilter = string.Empty;

    public LibraryPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadLibraryAsync();
    }

    private async Task LoadLibraryAsync()
    {
        try
        {
            var saved = await AppServices.Database.GetLibraryAsync();
            PopulateLibrary(saved);

            var history = await AppServices.Database.GetHistoryAsync(5);
            PopulateHistory(history);

            // Pull cloud library in background if user is authenticated
            if (!string.IsNullOrEmpty(AppServices.Settings.UserId))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var (token, _) = CredentialStore.Load();
                        if (!string.IsNullOrEmpty(token))
                        {
                            var count = await AppServices.Sync.PullLibraryAsync(AppServices.Settings.UserId, token);
                            if (count > 0)
                            {
                                var refreshed = await AppServices.Database.GetLibraryAsync();
                                DispatcherQueue?.TryEnqueue(() => PopulateLibrary(refreshed));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLog.Warn("LibraryPage.CloudPull", ex);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("LibraryPage.LoadLibraryAsync", ex);
            Nav.Toast("Couldn't load library");
        }
    }

    private void PopulateLibrary(List<LibraryEntity> saved)
    {
        _allManga.Clear();
        if (saved.Count > 0)
        {
            foreach (var s in saved)
            {
                _allManga.Add(new Manga(
                    s.MangaId,
                    s.Title,
                    s.CoverUrl ?? "#1A2433",
                    0,
                    false,
                    true,
                    s.Provider));
            }
        }
        else
        {
            _allManga.AddRange(Sample.Library);
        }
        ApplyFilters();
        if (CategoryAll != null) CategoryAll.Content = $"All · {_allManga.Count}";
    }

    private void PopulateHistory(List<HistoryEntity> history)
    {
        Continue.Clear();
        if (history.Count > 0)
        {
            foreach (var h in history)
            {
                var manga = _allManga.FirstOrDefault(m => m.Id == h.MangaId)
                    ?? new Manga(h.MangaId, h.MangaTitle, h.CoverUrl ?? "#1A2433", 0, false, true, h.Provider);
                Continue.Add(new ContinueItem(manga, h.ChapterTitle ?? h.ChapterId, 0.5));
            }
        }
    }

    private void ApplyFilters()
    {
        var filtered = _allManga.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(_searchFilter))
        {
            filtered = filtered.Where(m => m.Title.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase));
        }

        filtered = _sortMode switch
        {
            1 => filtered.OrderBy(m => m.Title),
            2 => filtered.OrderByDescending(m => m.Unread),
            _ => filtered
        };

        Items.Clear();
        foreach (var m in filtered) Items.Add(m);
    }

    private void OnSearchSubmitted(object? sender, string query)
    {
        _searchFilter = query ?? string.Empty;
        ApplyFilters();
    }

    private void OnSortClick(object sender, RoutedEventArgs e)
    {
        _sortMode = (_sortMode + 1) % 3;
        SortButton.Content = _sortMode switch
        {
            1 => "Sort: Title A–Z",
            2 => "Sort: Unread",
            _ => "Sort: Last read"
        };
        ApplyFilters();
    }

    private void OnCategoryChecked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Content is string text)
        {
            _currentCategory = text.Split('·')[0].Trim();
            ApplyFilters();
        }
    }

    private void OnOpenManga(object sender, RoutedEventArgs e) =>
        Nav.Go(typeof(MangaDetailPage), ((FrameworkElement)sender).DataContext ?? Sample.HollowCrown);

    private void OnResume(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is ContinueItem ci)
        {
            Nav.Go(typeof(ReaderPage), ci.Manga);
        }
        else
        {
            Nav.Go(typeof(ReaderPage), _allManga.FirstOrDefault() ?? Sample.HollowCrown);
        }
    }

    private async void OnUpdateLibrary(object sender, RoutedEventArgs e)
    {
        Nav.Toast("Checking library for new chapters...");
        try
        {
            var result = await AppServices.UpdateChecker.CheckUpdatesAsync(
                autoDownload: AppServices.Settings.AutoDownloadNew);

            AppServices.Settings.LastUpdateCheck = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            AppServices.Settings.Save();

            if (result.NewChaptersFound > 0)
            {
                Nav.Toast($"Found {result.NewChaptersFound} new chapters across {result.TotalChecked} titles!");
            }
            else
            {
                Nav.Toast("Library is up to date");
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("LibraryPage.OnUpdateLibrary", ex);
            Nav.Toast("Couldn't update library");
        }
    }
}
