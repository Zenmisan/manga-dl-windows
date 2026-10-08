using System.Collections.ObjectModel;
using MangaDl.Core;
using MangaDl.Core.Database.Entities;
using MangaDl.Core.Local;
using MangaDl.Dialogs;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages;

public sealed partial class LibraryPage : Page
{
    public ObservableCollection<Manga> Items { get; } = new(Sample.Library);
    public ObservableCollection<ContinueItem> Continue { get; } = new(Sample.ContinueReading);

    private readonly List<Manga> _allManga = [];
    private readonly List<CategoryEntity> _categories = [];
    private readonly Dictionary<string, HashSet<string>> _mangaCategoryMap = new(StringComparer.OrdinalIgnoreCase);

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
            var cats = await AppServices.Database.GetCategoriesAsync();
            var libCats = await AppServices.Database.GetAllLibraryCategoriesAsync();

            _categories.Clear();
            _categories.AddRange(cats);

            _mangaCategoryMap.Clear();
            foreach (var lc in libCats)
            {
                if (!_mangaCategoryMap.TryGetValue(lc.LibraryId, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _mangaCategoryMap[lc.LibraryId] = set;
                }
                set.Add(lc.CategoryId);
            }

            PopulateLibrary(saved);

            var history = await AppServices.Database.GetHistoryAsync(5);
            PopulateHistory(history);

            PopulateCategoryChips();

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
    }

    private void PopulateCategoryChips()
    {
        if (CategoriesStack == null) return;

        CategoriesStack.Children.Clear();

        var allRadio = new RadioButton
        {
            Content = $"All · {_allManga.Count}",
            GroupName = "LibCategory",
            Style = Application.Current.Resources.TryGetValue("ChipRadio", out var styleObj) ? (Style)styleObj : null,
            IsChecked = string.Equals(_currentCategory, "All", StringComparison.OrdinalIgnoreCase),
            Tag = "all"
        };
        allRadio.Checked += OnCategoryChecked;
        CategoriesStack.Children.Add(allRadio);

        foreach (var cat in _categories)
        {
            var slug = cat.Id;
            var count = _allManga.Count(m =>
            {
                if (string.Equals(slug, "local_files", StringComparison.OrdinalIgnoreCase) && m.Source == LocalImportService.Provider)
                {
                    return true;
                }
                return _mangaCategoryMap.TryGetValue($"{m.Source}/{m.Id}", out var set) &&
                       (set.Contains(slug) || set.Contains(cat.Name));
            });

            var rb = new RadioButton
            {
                Content = count > 0 ? $"{cat.Name} · {count}" : cat.Name,
                GroupName = "LibCategory",
                Style = Application.Current.Resources.TryGetValue("ChipRadio", out var st) ? (Style)st : null,
                IsChecked = string.Equals(_currentCategory, cat.Name, StringComparison.OrdinalIgnoreCase),
                Tag = cat.Id
            };
            rb.Checked += OnCategoryChecked;
            CategoriesStack.Children.Add(rb);
        }
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

        if (!string.Equals(_currentCategory, "All", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(_currentCategory, "Local files", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(m =>
                    m.Source == LocalImportService.Provider ||
                    (_mangaCategoryMap.TryGetValue($"{m.Source}/{m.Id}", out var set) &&
                     (set.Contains("local_files") || set.Contains("local-files"))));
            }
            else
            {
                var slug = FileNaming.Slugify(_currentCategory);
                filtered = filtered.Where(m =>
                    _mangaCategoryMap.TryGetValue($"{m.Source}/{m.Id}", out var set) &&
                    (set.Contains(slug) || set.Contains(_currentCategory)));
            }
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

    private static Manga? GetMangaFromContext(object sender)
    {
        if (sender is FrameworkElement fe && fe.DataContext is Manga m1) return m1;
        if (sender is MenuFlyoutItem mfi)
        {
            if (mfi.DataContext is Manga m2) return m2;
            if (mfi.Parent is MenuFlyout mf && mf.Target is FrameworkElement target && target.DataContext is Manga m3) return m3;
        }
        return null;
    }

    private async void OnSetCategoriesContext(object sender, RoutedEventArgs e)
    {
        if (GetMangaFromContext(sender) is Manga manga)
        {
            var saved = await CategoryDialogHelper.ShowAsync(XamlRoot, manga.Source, manga.Id, manga.Title);
            if (saved)
            {
                await LoadLibraryAsync();
                Nav.Toast($"Categories updated for \"{manga.Title}\"");
            }
        }
    }

    private void OnOpenMangaContext(object sender, RoutedEventArgs e)
    {
        if (GetMangaFromContext(sender) is Manga manga)
        {
            Nav.Go(typeof(MangaDetailPage), manga);
        }
    }

    private void OnResumeContext(object sender, RoutedEventArgs e)
    {
        if (GetMangaFromContext(sender) is Manga manga)
        {
            var isNovel = AppServices.Extensions.IsNovel(manga.Source) || AppServices.Extensions.GetExtension(manga.Source)?.Type == "novel";
            Nav.Go(isNovel ? typeof(NovelReaderPage) : typeof(ReaderPage), manga);
        }
    }

    private async void OnRemoveFromLibraryContext(object sender, RoutedEventArgs e)
    {
        if (GetMangaFromContext(sender) is Manga manga)
        {
            try
            {
                await AppServices.Database.RemoveFromLibraryAsync(manga.Source, manga.Id);
                var libEntity = new LibraryEntity
                {
                    Provider = manga.Source,
                    MangaId = manga.Id,
                    Title = manga.Title,
                    CoverUrl = manga.Cover
                };
                _ = AppServices.Sync.SyncMangaSubscriptionAsync(libEntity, subscribed: false);
                Nav.Toast($"Removed \"{manga.Title}\" from library");
                await LoadLibraryAsync();
            }
            catch (Exception ex)
            {
                AppLog.Warn("LibraryPage.OnRemoveFromLibraryContext", ex);
                Nav.Toast("Couldn't remove from library");
            }
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
