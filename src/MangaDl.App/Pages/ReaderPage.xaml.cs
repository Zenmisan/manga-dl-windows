using System.IO;
using MangaDl.Core;
using MangaDl.Core.Database.Entities;
using MangaDl.Core.Extensions;
using MangaDl.Core.Local;
using MangaDl.Helpers;
using MangaDl.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage.Streams;
using Windows.System;

namespace MangaDl.Pages;

public sealed partial class ReaderPage : Page
{
    private Manga _currentManga = Sample.HollowCrown;
    private string _chapterId = "1";
    private string _chapterTitle = "Chapter 1";
    private double _chapterNumber = 1.0;

    private readonly List<Chapter> _chapters = [];
    private int _currentChapterIndex = 0;

    private readonly List<MangaPageInfo> _pages = [];
    private int _page = 1;
    private int _pageTotal = 1;

    private bool _spread = true;
    private bool _isWebtoon = false;
    private bool _bookmarked = false;
    private bool _syncing = false;

    public ReaderPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Nav.SetTitle($"{_currentManga.Title} — {_chapterTitle}");
            ApplyReaderPreferences();
            Focus(FocusState.Programmatic);
        };
    }

    private void ApplyReaderPreferences()
    {
        var s = AppServices.Settings;

        // Background color
        Background = s.ReaderBackground switch
        {
            "Gray" => new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 30, 30)),
            "White" => new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 245, 245, 245)),
            _ => new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 10, 10, 10))
        };

        // Page number display
        if (PageCount is not null)
        {
            PageCount.Visibility = s.ShowPageNumber ? Visibility.Visible : Visibility.Collapsed;
        }

        // Fullscreen
        if (s.OpenFullscreen)
        {
            var window = App.Window?.AppWindow;
            if (window is not null && window.Presenter.Kind != AppWindowPresenterKind.FullScreen)
            {
                window.SetPresenter(AppWindowPresenterKind.FullScreen);
            }
        }

        // Drawer toggles
        if (DrawerCropBorders is not null) DrawerCropBorders.IsChecked = s.CropBorders;
        if (DrawerTapZones is not null) DrawerTapZones.IsChecked = s.ClickZones;

        // Initial mode
        if (s.ReaderMode == "Webtoon" || s.ReaderMode == "Vertical")
        {
            SetLayoutMode("webtoon");
        }
        else
        {
            SetLayoutMode(_spread ? "spread" : "single");
        }
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _spread = AppServices.Settings.TwoPageSpread;

        if (e.Parameter is ReaderNavigationArgs args)
        {
            _currentManga = args.Manga;
            _chapterId = args.ChapterId ?? "1";
            _chapterTitle = args.ChapterTitle ?? $"Chapter {args.ChapterNumber}";
            _chapterNumber = args.ChapterNumber;
            _page = Math.Max(1, args.StartPage);
        }
        else if (e.Parameter is Manga m)
        {
            _currentManga = m;
        }
        else if (e.Parameter as string == "single")
        {
            _spread = false;
            if (SettingsToggle is not null) SettingsToggle.IsChecked = false;
        }

        ApplyReaderPreferences();
        await InitializeMangaReaderAsync();
    }

    private async Task InitializeMangaReaderAsync()
    {
        if (LoadingRing != null)
        {
            LoadingRing.Visibility = Visibility.Visible;
            LoadingRing.IsActive = true;
        }

        try
        {
            ReaderMangaTitle.Text = _currentManga.Title;
            ReaderChapterTitle.Text = $"  ·  {_chapterTitle}";
            Nav.SetTitle($"{_currentManga.Title} — {_chapterTitle}");

            // Load chapter index
            _chapters.Clear();
            if (_currentManga.Source == LocalImportService.Provider)
            {
                var downloads = await AppServices.Database.GetDownloadsAsync();
                var local = downloads
                    .Where(d => d.Provider == LocalImportService.Provider && d.MangaId == _currentManga.Id)
                    .OrderBy(d => d.ChapterNumber)
                    .ToList();

                for (var i = 0; i < local.Count; i++)
                {
                    _chapters.Add(new Chapter(
                        local[i].ChapterNumber.ToString(),
                        local[i].ChapterTitle ?? $"Chapter {local[i].ChapterNumber}",
                        "",
                        "Local",
                        "Downloaded",
                        Read: false));
                }
            }
            else
            {
                var detail = await AppServices.Extensions.GetMangaDetailAsync(_currentManga.Source, _currentManga.Id);
                if (detail != null && detail.Chapters.Count > 0)
                {
                    foreach (var ch in detail.Chapters)
                    {
                        _chapters.Add(new Chapter(
                            ch.Number,
                            ch.Title ?? $"Chapter {ch.Number}",
                            ch.Released,
                            ch.Scanlator ?? _currentManga.Source,
                            "Online",
                            Read: false));
                    }
                }
            }

            // Find current chapter position
            _currentChapterIndex = _chapters.FindIndex(c =>
                c.Number == _chapterId ||
                (double.TryParse(c.Number, out var n) && Math.Abs(n - _chapterNumber) < 0.001));

            if (_currentChapterIndex < 0 && _chapters.Count > 0)
            {
                _currentChapterIndex = 0;
            }

            await LoadChapterPagesAsync(_chapterId, _chapterNumber, _chapterTitle);
        }
        catch (Exception ex)
        {
            AppLog.Warn("ReaderPage.InitializeMangaReaderAsync", ex);
            Nav.Toast("Couldn't load manga pages");
        }
        finally
        {
            if (LoadingRing != null)
            {
                LoadingRing.Visibility = Visibility.Collapsed;
                LoadingRing.IsActive = false;
            }
        }
    }

    private async Task LoadChapterPagesAsync(string chapterId, double chapterNumber, string chapterTitle)
    {
        _pages.Clear();

        // 1. Try local downloaded CBZ first
        var localCbz = MangaPageLoader.FindLocalChapterCbz(AppServices.Settings.DownloadPath, _currentManga.Title, chapterNumber, chapterId);
        if (localCbz != null && File.Exists(localCbz))
        {
            var extracted = MangaPageLoader.ExtractPagesFromCbz(localCbz);
            _pages.AddRange(extracted);
        }
        else if (_currentManga.Source != LocalImportService.Provider)
        {
            // 2. Fetch from online extension scraper
            try
            {
                var urls = await AppServices.Extensions.GetPagesAsync(_currentManga.Source, chapterId);
                for (var i = 0; i < urls.Count; i++)
                {
                    _pages.Add(new MangaPageInfo(i + 1, ImageUrl: urls[i]));
                }
            }
            catch (Exception ex)
            {
                AppLog.Warn("ReaderPage.GetPagesAsync", ex);
            }
        }

        _pageTotal = Math.Max(1, _pages.Count);
        _page = Math.Clamp(_page, 1, _pageTotal);

        if (PageSlider != null)
        {
            PageSlider.Maximum = _pageTotal;
            PageSlider.Value = _page;
        }
        if (SliderPageTotal != null)
        {
            SliderPageTotal.Text = _pageTotal.ToString();
        }

        if (_isWebtoon)
        {
            RenderWebtoonStrip();
        }
        else
        {
            ShowPage();
        }
    }

    // ----- Layout -----
    private void OnLayoutChecked(object sender, RoutedEventArgs e)
    {
        if (sender == WebtoonOption)
            SetLayoutMode("webtoon");
        else if (sender == SingleOption)
            SetLayoutMode("single");
        else
            SetLayoutMode("spread");
    }

    private void OnDualToggled(object sender, RoutedEventArgs e) =>
        SetLayoutMode(((ToggleButton)sender).IsChecked == true ? "spread" : "single");

    private void SetLayoutMode(string mode)
    {
        if (_syncing || SpreadView is null || SingleView is null || WebtoonView is null) return;
        _syncing = true;

        switch (mode.ToLowerInvariant())
        {
            case "webtoon":
                _isWebtoon = true;
                _spread = false;
                WebtoonView.Visibility = Visibility.Visible;
                SpreadView.Visibility = Visibility.Collapsed;
                SingleView.Visibility = Visibility.Collapsed;
                if (WebtoonOption != null) WebtoonOption.IsChecked = true;
                if (DualToggle != null) DualToggle.IsChecked = false;
                RenderWebtoonStrip();
                break;
            case "single":
                _isWebtoon = false;
                _spread = false;
                WebtoonView.Visibility = Visibility.Collapsed;
                SpreadView.Visibility = Visibility.Collapsed;
                SingleView.Visibility = Visibility.Visible;
                if (SingleOption != null) SingleOption.IsChecked = true;
                if (DualToggle != null) DualToggle.IsChecked = false;
                ShowPage();
                break;
            default: // "spread"
                _isWebtoon = false;
                _spread = true;
                WebtoonView.Visibility = Visibility.Collapsed;
                SpreadView.Visibility = Visibility.Visible;
                SingleView.Visibility = Visibility.Collapsed;
                if (SpreadOption != null) SpreadOption.IsChecked = true;
                if (DualToggle != null) DualToggle.IsChecked = true;
                ShowPage();
                break;
        }

        _syncing = false;
    }

    // ----- Image Binding -----
    private static async Task SetImageSourceAsync(Image targetImage, MangaPageInfo? page)
    {
        if (page == null)
        {
            targetImage.Source = null;
            return;
        }

        try
        {
            if (page.ImageBytes != null && page.ImageBytes.Length > 0)
            {
                var bitmap = new BitmapImage();
                using var stream = new InMemoryRandomAccessStream();
                using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
                {
                    writer.WriteBytes(page.ImageBytes);
                    await writer.StoreAsync();
                }
                await bitmap.SetSourceAsync(stream);
                targetImage.Source = bitmap;
            }
            else if (!string.IsNullOrEmpty(page.ImageUrl))
            {
                targetImage.Source = new BitmapImage(new Uri(page.ImageUrl));
            }
            else
            {
                targetImage.Source = null;
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("ReaderPage.SetImageSourceAsync", ex);
        }
    }

    // ----- Paging -----
    private void ShowPage()
    {
        if (PageCount is null || SliderPage is null || PageSlider is null) return;

        PageCount.Text = _spread ? $"{_page}–{Math.Min(_page + 1, _pageTotal)} / {_pageTotal}" : $"{_page} / {_pageTotal}";
        SliderPage.Text = _page.ToString();
        if ((int)PageSlider.Value != _page) PageSlider.Value = _page;

        // Render images
        if (_spread)
        {
            var pRight = _pages.ElementAtOrDefault(_page - 1);
            var pLeft = _pages.ElementAtOrDefault(_page); // Next page on left in RTL manga
            _ = SetImageSourceAsync(RightImage, pRight);
            _ = SetImageSourceAsync(LeftImage, pLeft);
        }
        else
        {
            var pSingle = _pages.ElementAtOrDefault(_page - 1);
            _ = SetImageSourceAsync(SingleImage, pSingle);
        }

        RecordProgress();
    }

    private void RenderWebtoonStrip()
    {
        if (WebtoonPagesStack == null) return;
        WebtoonPagesStack.Children.Clear();

        var paddingPercent = AppServices.Settings.WebtoonSidePadding;
        var widthRatio = Math.Clamp(1.0 - (paddingPercent * 2.0 / 100.0), 0.3, 1.0);

        foreach (var p in _pages)
        {
            var border = new Border
            {
                Background = X.Hex("#111111"),
                CornerRadius = new CornerRadius(0),
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 900 * widthRatio,
                Margin = new Thickness(0, 0, 0, 4),
            };
            var img = new Image
            {
                Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            border.Child = img;
            WebtoonPagesStack.Children.Add(border);
            _ = SetImageSourceAsync(img, p);
        }

        RecordProgress();
    }

    private void RecordProgress()
    {
        try
        {
            _ = AppServices.Database.SaveProgressAsync(_currentManga.Source, _currentManga.Id, _chapterId, _chapterNumber, _page, _page >= _pageTotal)
                .ContinueWith(t => AppLog.Warn("ReaderPage.SaveProgressAsync", t.Exception!), TaskContinuationOptions.OnlyOnFaulted);
            _ = AppServices.Database.RecordHistoryAsync(_currentManga.Source, _currentManga.Id, _currentManga.Title, _chapterId, $"{_chapterTitle} · page {_page} of {_pageTotal}", _currentManga.Cover)
                .ContinueWith(t => AppLog.Warn("ReaderPage.RecordHistoryAsync", t.Exception!), TaskContinuationOptions.OnlyOnFaulted);
            _ = AppServices.Sync.SyncChapterReadAsync(
                _currentManga.Source,
                _currentManga.Id,
                _chapterId,
                _page,
                _currentManga.Title,
                $"{_chapterTitle} · page {_page} of {_pageTotal}",
                isCompleted: _page >= _pageTotal);
            _ = AppServices.SyncTrackerProgressAsync(
                _currentManga.Source,
                _currentManga.Id,
                _currentManga.Title,
                _chapterNumber,
                isCompleted: _page >= _pageTotal);
        }
        catch (Exception ex)
        {
            AppLog.Warn("ReaderPage.RecordProgress", ex);
        }
    }

    private void Go(int delta)
    {
        _page = Math.Clamp(_page + delta * (_spread ? 2 : 1), 1, _pageTotal);
        ShowPage();
    }

    private void OnPrevPage(object sender, RoutedEventArgs e) => Go(-1);
    private void OnNextPage(object sender, RoutedEventArgs e) => Go(1);

    private async void OnPrevChapter(object sender, RoutedEventArgs e)
    {
        if (_currentChapterIndex > 0 && _chapters.Count > 0)
        {
            _currentChapterIndex--;
            var ch = _chapters[_currentChapterIndex];
            _chapterId = ch.Number;
            _chapterTitle = ch.Title;
            double.TryParse(ch.Number, out var num);
            _chapterNumber = num > 0 ? num : 1.0;
            _page = 1;

            ReaderMangaTitle.Text = _currentManga.Title;
            ReaderChapterTitle.Text = $"  ·  {_chapterTitle}";
            Nav.Toast($"Opened {_chapterTitle}");
            await LoadChapterPagesAsync(_chapterId, _chapterNumber, _chapterTitle);
        }
        else
        {
            Nav.Toast("No previous chapter");
        }
    }

    private async void OnNextChapter(object sender, RoutedEventArgs e)
    {
        if (_currentChapterIndex < _chapters.Count - 1 && _chapters.Count > 0)
        {
            _currentChapterIndex++;
            var ch = _chapters[_currentChapterIndex];
            _chapterId = ch.Number;
            _chapterTitle = ch.Title;
            double.TryParse(ch.Number, out var num);
            _chapterNumber = num > 0 ? num : 1.0;
            _page = 1;

            ReaderMangaTitle.Text = _currentManga.Title;
            ReaderChapterTitle.Text = $"  ·  {_chapterTitle}";
            Nav.Toast($"Opened {_chapterTitle}");
            await LoadChapterPagesAsync(_chapterId, _chapterNumber, _chapterTitle);
        }
        else
        {
            Nav.Toast("No next chapter");
        }
    }

    private void OnSliderChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        _page = (int)e.NewValue;
        ShowPage();
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Right:
            case VirtualKey.Space:
                Go(1);
                break;
            case VirtualKey.Left:
                Go(-1);
                break;
            case VirtualKey.PageDown:
                Go(1);
                break;
            case VirtualKey.PageUp:
                Go(-1);
                break;
            case VirtualKey.F:
                ToggleFullscreen();
                break;
            case VirtualKey.Escape:
                Close();
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    // ----- Top bar -----
    private void OnBookmark(object sender, RoutedEventArgs e)
    {
        _bookmarked = !_bookmarked;
        BookmarkIcon.Kind = _bookmarked ? "BookmarkFilled" : "Bookmark";
        BookmarkIcon.Foreground = X.Res(_bookmarked ? "AccentLightBrush" : "FgBrush");
    }

    private void OnSettingsToggled(object sender, RoutedEventArgs e)
    {
        if (SettingsPanel is not null) SettingsPanel.Visibility = X.Vis(((ToggleButton)sender).IsChecked == true);
    }

    private void OnFullscreen(object sender, RoutedEventArgs e) => ToggleFullscreen();

    private static void ToggleFullscreen()
    {
        var window = App.Window?.AppWindow;
        if (window is null) return;
        window.SetPresenter(window.Presenter.Kind == AppWindowPresenterKind.FullScreen
            ? AppWindowPresenterKind.Overlapped
            : AppWindowPresenterKind.FullScreen);
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private static void Close()
    {
        var window = App.Window?.AppWindow;
        if (window?.Presenter.Kind == AppWindowPresenterKind.FullScreen) window.SetPresenter(AppWindowPresenterKind.Overlapped);
        Nav.Back();
    }

    // ----- Filters (labels only) -----
    private void OnBrightness(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (BrightnessText is not null) BrightnessText.Text = $"{(int)e.NewValue}%";
    }

    private void OnContrast(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (ContrastText is not null) ContrastText.Text = $"{(int)e.NewValue}%";
    }
}
