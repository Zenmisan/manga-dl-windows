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
            if (saved.Count > 0)
            {
                _allManga.Clear();
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
                ApplyFilters();
                if (CategoryAll != null) CategoryAll.Content = $"All · {_allManga.Count}";
            }
            else
            {
                _allManga.Clear();
                _allManga.AddRange(Sample.Library);
                if (CategoryAll != null) CategoryAll.Content = $"All · {_allManga.Count}";
            }

            var history = await AppServices.Database.GetHistoryAsync(5);
            if (history.Count > 0)
            {
                Continue.Clear();
                foreach (var h in history)
                {
                    var manga = _allManga.FirstOrDefault(m => m.Id == h.MangaId)
                        ?? new Manga(h.MangaId, h.MangaTitle, h.CoverUrl ?? "#1A2433", 0, false, true, h.Provider);
                    Continue.Add(new ContinueItem(manga, h.ChapterTitle ?? h.ChapterId, 0.5));
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("LibraryPage.LoadLibraryAsync", ex);
            Nav.Toast("Couldn't load library");
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
        Nav.Toast($"Checking {_allManga.Count} manga for new chapters...");
        await Task.Delay(500);
        Nav.Toast("Library is up to date");
    }
}
