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
    private Manga _currentManga = new("", "", "#1E293B", "", "0 ch", false);
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
    private bool _updatingFilters = false;

    private record WebtoonItemSlot(Border Container, Image Img, MangaPageInfo Page, TextBlock PlaceholderText)
    {
        public bool IsLoaded { get; set; }
    }

    private readonly List<WebtoonItemSlot> _webtoonSlots = [];

    public ReaderPage()
    {
        InitializeComponent();
        if (WebtoonView != null)
        {
            WebtoonView.ViewChanged += OnWebtoonViewChanged;
        }
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

        _updatingFilters = true;
        try
        {
            if (AmbilightToggle != null) AmbilightToggle.IsChecked = s.AmbilightGlow;
            if (FilmstripToggle != null) FilmstripToggle.IsChecked = s.ShowFilmstrip;
            if (SepiaToggle != null) SepiaToggle.IsChecked = s.ImageSepia;
            if (InvertToggle != null) InvertToggle.IsChecked = s.ImageInvert;
            if (GrayscaleToggle != null) GrayscaleToggle.IsChecked = s.ImageGrayscale;
            if (BrightnessSlider != null) BrightnessSlider.Value = s.ImageBrightness + 100;
            if (ContrastSlider != null) ContrastSlider.Value = s.ImageContrast + 100;
            if (BrightnessText != null) BrightnessText.Text = $"{s.ImageBrightness + 100}%";
            if (ContrastText != null) ContrastText.Text = $"{s.ImageContrast + 100}%";
        }
        finally
        {
            _updatingFilters = false;
        }

        ApplyImageFilters();

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

        if (string.IsNullOrWhiteSpace(_currentManga.Title))
        {
            if (ReaderStatusBanner != null)
            {
                ReaderStatusBanner.Visibility = Visibility.Visible;
                ReaderStatusTitle.Text = "No Chapter Selected";
                ReaderStatusMessage.Text = "Navigate from the Library or Browse page to select a manga to read.";
            }
            return;
        }

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

            PopulateChapterDropdown();
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

        UpdateFilmstrip();

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
        if (ReferenceEquals(sender, WebtoonOption))
            SetLayoutMode("webtoon");
        else if (ReferenceEquals(sender, SingleOption))
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
                if (BottomControlsOverlay != null) BottomControlsOverlay.Visibility = Visibility.Collapsed;
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
                if (BottomControlsOverlay != null) BottomControlsOverlay.Visibility = Visibility.Visible;
                if (SingleOption != null) SingleOption.IsChecked = true;
                if (DualToggle != null) DualToggle.IsChecked = false;
                ShowPage();
                UpdateFilmstrip();
                break;
            default: // "spread"
                _isWebtoon = false;
                _spread = true;
                WebtoonView.Visibility = Visibility.Collapsed;
                SpreadView.Visibility = Visibility.Visible;
                SingleView.Visibility = Visibility.Collapsed;
                if (BottomControlsOverlay != null) BottomControlsOverlay.Visibility = Visibility.Visible;
                if (SpreadOption != null) SpreadOption.IsChecked = true;
                if (DualToggle != null) DualToggle.IsChecked = true;
                ShowPage();
                UpdateFilmstrip();
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
                if (page.ImageUrl.Contains("asura-descramble", StringComparison.OrdinalIgnoreCase) ||
                    page.ImageUrl.Contains("#") ||
                    page.ImageUrl.StartsWith("/"))
                {
                    var bytes = await AppServices.Http.FetchBytesAsync(page.ImageUrl);
                    var bitmap = new BitmapImage();
                    using var stream = new InMemoryRandomAccessStream();
                    using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
                    {
                        writer.WriteBytes(bytes);
                        await writer.StoreAsync();
                    }
                    await bitmap.SetSourceAsync(stream);
                    targetImage.Source = bitmap;
                }
                else
                {
                    targetImage.Source = new BitmapImage(new Uri(page.ImageUrl));
                }
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

        UpdateFilmstripActivePage();
        RecordProgress();
    }

    private void RenderWebtoonStrip()
    {
        if (WebtoonPagesStack == null || WebtoonView == null) return;
        WebtoonPagesStack.Children.Clear();
        _webtoonSlots.Clear();

        var paddingPercent = AppServices.Settings.WebtoonSidePadding;
        var widthRatio = Math.Clamp(1.0 - (paddingPercent * 2.0 / 100.0), 0.3, 1.0);

        for (int i = 0; i < _pages.Count; i++)
        {
            var p = _pages[i];
            var border = new Border
            {
                Background = X.Hex("#111111"),
                CornerRadius = new CornerRadius(0),
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 900 * widthRatio,
                MinHeight = 850,
                Margin = new Thickness(0, 0, 0, 4),
            };

            var grid = new Grid();
            var placeholder = new TextBlock
            {
                Text = $"Page {i + 1}",
                FontSize = 13,
                Foreground = X.Hex("#444444"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(placeholder);

            var img = new Image
            {
                Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            grid.Children.Add(img);

            border.Child = grid;
            WebtoonPagesStack.Children.Add(border);

            _webtoonSlots.Add(new WebtoonItemSlot(border, img, p, placeholder));
        }

        UpdateWebtoonVisibleWindow();
        RecordProgress();
    }

    private void OnWebtoonViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        UpdateWebtoonVisibleWindow();
    }

    private void UpdateWebtoonVisibleWindow()
    {
        if (!_isWebtoon || _webtoonSlots.Count == 0 || WebtoonView == null) return;

        var scrollY = WebtoonView.VerticalOffset;
        var viewH = WebtoonView.ViewportHeight > 0 ? WebtoonView.ViewportHeight : 800;

        var windowTop = scrollY - (viewH * 2.5);
        var windowBottom = scrollY + (viewH * 3.5);

        double accumY = 0;
        int activePage = 1;

        for (int i = 0; i < _webtoonSlots.Count; i++)
        {
            var slot = _webtoonSlots[i];
            var itemH = slot.Container.ActualHeight > 0 ? slot.Container.ActualHeight : 850;
            var itemTop = accumY;
            var itemBottom = accumY + itemH;

            if (scrollY >= itemTop - (viewH * 0.4) && scrollY < itemBottom)
            {
                activePage = i + 1;
            }

            bool inWindow = itemBottom >= windowTop && itemTop <= windowBottom;

            if (inWindow && !slot.IsLoaded)
            {
                slot.IsLoaded = true;
                slot.PlaceholderText.Visibility = Visibility.Collapsed;
                _ = SetImageSourceAsync(slot.Img, slot.Page);
            }
            else if (!inWindow && slot.IsLoaded)
            {
                slot.IsLoaded = false;
                slot.Img.Source = null;
                slot.PlaceholderText.Visibility = Visibility.Visible;
            }

            accumY += itemH + 4;
        }

        if (activePage != _page)
        {
            _page = activePage;
            if (PageCount != null) PageCount.Text = $"{_page} / {_pageTotal}";
            if (SliderPage != null) SliderPage.Text = _page.ToString();
        }
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
            if (ChapterDropdown != null)
            {
                ChapterDropdown.SelectionChanged -= OnChapterDropdownChanged;
                ChapterDropdown.SelectedIndex = _currentChapterIndex;
                ChapterDropdown.SelectionChanged += OnChapterDropdownChanged;
            }
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
            if (ChapterDropdown != null)
            {
                ChapterDropdown.SelectionChanged -= OnChapterDropdownChanged;
                ChapterDropdown.SelectedIndex = _currentChapterIndex;
                ChapterDropdown.SelectionChanged += OnChapterDropdownChanged;
            }
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
        var open = ((ToggleButton)sender).IsChecked == true;
        if (SettingsPanel is not null) SettingsPanel.Visibility = X.Vis(open);
        if (open)
        {
            if (CommentsDrawer != null) CommentsDrawer.Visibility = Visibility.Collapsed;
            if (CommentsToggle != null) CommentsToggle.IsChecked = false;
        }
    }

    private void OnCommentsToggled(object sender, RoutedEventArgs e)
    {
        var open = CommentsToggle?.IsChecked == true;
        if (CommentsDrawer != null) CommentsDrawer.Visibility = X.Vis(open);
        if (open)
        {
            if (SettingsPanel != null) SettingsPanel.Visibility = Visibility.Collapsed;
            if (SettingsToggle != null) SettingsToggle.IsChecked = false;
            _ = LoadChapterCommentsAsync();
        }
    }

    private void OnCloseCommentsDrawer(object sender, RoutedEventArgs e)
    {
        if (CommentsDrawer != null) CommentsDrawer.Visibility = Visibility.Collapsed;
        if (CommentsToggle != null) CommentsToggle.IsChecked = false;
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

    // ----- Chapter Dropdown -----
    private void PopulateChapterDropdown()
    {
        if (ChapterDropdown == null) return;
        ChapterDropdown.SelectionChanged -= OnChapterDropdownChanged;
        ChapterDropdown.Items.Clear();

        for (int i = 0; i < _chapters.Count; i++)
        {
            var ch = _chapters[i];
            ChapterDropdown.Items.Add(new ComboBoxItem
            {
                Content = $"Ch. {ch.Number} - {ch.Title}",
                Tag = ch
            });
        }

        if (_currentChapterIndex >= 0 && _currentChapterIndex < ChapterDropdown.Items.Count)
        {
            ChapterDropdown.SelectedIndex = _currentChapterIndex;
        }

        ChapterDropdown.SelectionChanged += OnChapterDropdownChanged;
    }

    private async void OnChapterDropdownChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ChapterDropdown?.SelectedItem is ComboBoxItem item && item.Tag is Chapter ch)
        {
            var idx = _chapters.IndexOf(ch);
            if (idx >= 0 && idx != _currentChapterIndex)
            {
                _currentChapterIndex = idx;
                _chapterId = ch.Number;
                _chapterTitle = ch.Title;
                if (double.TryParse(ch.Number, out var n)) _chapterNumber = n;
                _page = 1;

                ReaderChapterTitle.Text = $"  ·  {_chapterTitle}";
                Nav.SetTitle($"{_currentManga.Title} — {_chapterTitle}");

                if (LoadingRing != null)
                {
                    LoadingRing.Visibility = Visibility.Visible;
                    LoadingRing.IsActive = true;
                }

                try
                {
                    await LoadChapterPagesAsync(_chapterId, _chapterNumber, _chapterTitle);
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
        }
    }

    // ----- Filmstrip Scrubber -----
    private void UpdateFilmstrip()
    {
        if (FilmstripContainer == null || FilmstripThumbnailsStack == null) return;

        if (_pageTotal > 60 || !AppServices.Settings.ShowFilmstrip || _isWebtoon)
        {
            FilmstripContainer.Visibility = Visibility.Collapsed;
            return;
        }

        FilmstripContainer.Visibility = Visibility.Visible;
        FilmstripThumbnailsStack.Children.Clear();

        for (int i = 0; i < _pages.Count; i++)
        {
            int pageNum = i + 1;
            var isCurrent = pageNum == _page || (_spread && pageNum == _page + 1);

            var thumbBorder = new Border
            {
                Width = 36,
                Height = 52,
                CornerRadius = new CornerRadius(5),
                Background = X.Hex("#181818"),
                BorderBrush = isCurrent ? X.Hex("#6366f1") : X.Hex("#2a2a2a"),
                BorderThickness = new Thickness(isCurrent ? 2 : 1),
                Margin = new Thickness(1),
                Tag = pageNum
            };

            var grid = new Grid();
            var img = new Image
            {
                Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill
            };
            grid.Children.Add(img);

            var pageNumBadge = new Border
            {
                Background = X.Hex("#C0000000"),
                CornerRadius = new CornerRadius(3),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 2),
                Padding = new Thickness(3, 1, 3, 1)
            };
            pageNumBadge.Child = new TextBlock
            {
                Text = pageNum.ToString(),
                FontSize = 9,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = X.Hex("#e2e8f0")
            };
            grid.Children.Add(pageNumBadge);

            thumbBorder.Child = grid;

            // Load thumbnail image
            _ = SetImageSourceAsync(img, _pages[i]);

            // Jump to page on click
            thumbBorder.PointerPressed += (s, ev) =>
            {
                if (s is Border b && b.Tag is int p)
                {
                    JumpToPage(p);
                }
            };

            FilmstripThumbnailsStack.Children.Add(thumbBorder);
        }

        ScrollFilmstripToActive();
    }

    private void JumpToPage(int pageNum)
    {
        _page = Math.Clamp(pageNum, 1, _pageTotal);
        ShowPage();
    }

    private void UpdateFilmstripActivePage()
    {
        if (FilmstripThumbnailsStack == null || FilmstripContainer == null || FilmstripContainer.Visibility != Visibility.Visible) return;

        for (int i = 0; i < FilmstripThumbnailsStack.Children.Count; i++)
        {
            if (FilmstripThumbnailsStack.Children[i] is Border b && b.Tag is int p)
            {
                var isCurrent = p == _page || (_spread && p == _page + 1);
                b.BorderBrush = isCurrent ? X.Hex("#6366f1") : X.Hex("#2a2a2a");
                b.BorderThickness = new Thickness(isCurrent ? 2 : 1);
            }
        }

        ScrollFilmstripToActive();
    }

    private void ScrollFilmstripToActive()
    {
        if (FilmstripScrollViewer == null || FilmstripThumbnailsStack == null) return;
        var activeIdx = _page - 1;
        if (activeIdx >= 0 && activeIdx < FilmstripThumbnailsStack.Children.Count)
        {
            var targetOffset = Math.Max(0, (activeIdx * 44) - (FilmstripScrollViewer.ViewportWidth / 2) + 22);
            FilmstripScrollViewer.ChangeView(targetOffset, null, null);
        }
    }

    // ----- Filters & Adjustments -----
    private void OnBrightness(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (BrightnessText is not null) BrightnessText.Text = $"{(int)e.NewValue}%";
        if (_updatingFilters) return;
        AppServices.Settings.ImageBrightness = (int)e.NewValue - 100;
        AppServices.Settings.Save();
        ApplyImageFilters();
    }

    private void OnContrast(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (ContrastText is not null) ContrastText.Text = $"{(int)e.NewValue}%";
        if (_updatingFilters) return;
        AppServices.Settings.ImageContrast = (int)e.NewValue - 100;
        AppServices.Settings.Save();
        ApplyImageFilters();
    }

    private void OnFilterChipToggled(object sender, RoutedEventArgs e)
    {
        if (_updatingFilters) return;
        var s = AppServices.Settings;
        if (sender == GrayscaleToggle) s.ImageGrayscale = GrayscaleToggle?.IsChecked == true;
        else if (sender == InvertToggle) s.ImageInvert = InvertToggle?.IsChecked == true;
        else if (sender == SepiaToggle) s.ImageSepia = SepiaToggle?.IsChecked == true;
        s.Save();
        ApplyImageFilters();
    }

    private void OnAmbilightToggled(object sender, RoutedEventArgs e)
    {
        if (_updatingFilters) return;
        AppServices.Settings.AmbilightGlow = AmbilightToggle?.IsChecked == true;
        AppServices.Settings.Save();
        ApplyImageFilters();
    }

    private void OnFilmstripToggled(object sender, RoutedEventArgs e)
    {
        if (_updatingFilters) return;
        AppServices.Settings.ShowFilmstrip = FilmstripToggle?.IsChecked == true;
        AppServices.Settings.Save();
        UpdateFilmstrip();
    }

    private void ApplyImageFilters()
    {
        var s = AppServices.Settings;
        var brightness = s.ImageBrightness;
        var sepia = s.ImageSepia;
        var invert = s.ImageInvert;
        var ambilight = s.AmbilightGlow;

        double dimOpacity = 0;
        double brightOpacity = 0;

        if (brightness < 0)
        {
            dimOpacity = Math.Abs(brightness) / 50.0 * 0.75;
        }
        else if (brightness > 0)
        {
            brightOpacity = brightness / 50.0 * 0.45;
        }

        double sepiaOpacity = sepia ? 0.28 : 0;
        double glowOpacity = ambilight ? 0.32 : 0;

        // Apply to Left Page
        if (LeftDimOverlay != null) LeftDimOverlay.Opacity = dimOpacity;
        if (LeftBrightOverlay != null) LeftBrightOverlay.Opacity = brightOpacity;
        if (LeftSepiaOverlay != null) LeftSepiaOverlay.Opacity = sepiaOpacity;
        if (LeftGlowBorder != null) LeftGlowBorder.Opacity = glowOpacity;

        // Apply to Right Page
        if (RightDimOverlay != null) RightDimOverlay.Opacity = dimOpacity;
        if (RightBrightOverlay != null) RightBrightOverlay.Opacity = brightOpacity;
        if (RightSepiaOverlay != null) RightSepiaOverlay.Opacity = sepiaOpacity;
        if (RightGlowBorder != null) RightGlowBorder.Opacity = glowOpacity;

        // Apply to Single Page
        if (SingleDimOverlay != null) SingleDimOverlay.Opacity = dimOpacity;
        if (SingleBrightOverlay != null) SingleBrightOverlay.Opacity = brightOpacity;
        if (SingleSepiaOverlay != null) SingleSepiaOverlay.Opacity = sepiaOpacity;
        if (SingleGlowBorder != null) SingleGlowBorder.Opacity = glowOpacity;

        // Invert effect background tone
        var bgTone = invert ? X.Hex("#000000") : X.Hex("#141414");
        if (LeftPageBorder != null) LeftPageBorder.Background = bgTone;
        if (RightPageBorder != null) RightPageBorder.Background = bgTone;
        if (SinglePageBorder != null) SinglePageBorder.Background = bgTone;
    }

    private async Task LoadChapterCommentsAsync()
    {
        try
        {
            var userStats = await AppServices.Database.GetAllCompletedProgressAsync();
            var userRank = GamificationService.GetHunterRank(userStats.Count * 10);
            var username = AppServices.Settings.UserName ?? "Hunter";
            if (DrawerPostingAsText != null) DrawerPostingAsText.Text = $"{username} ({userRank.Name})";

            var chId = !string.IsNullOrEmpty(_chapterId) ? _chapterId : _chapterNumber.ToString();
            var comments = await AppServices.Community.GetCommentsAsync(AppServices.Database, _manga.Source, _manga.Id, chId);

            if (DrawerCommentsTitle != null) DrawerCommentsTitle.Text = $"Comments ({comments.Count})";
            if (DrawerCommentsList == null) return;

            DrawerCommentsList.Children.Clear();
            if (comments.Count == 0)
            {
                DrawerCommentsList.Children.Add(new TextBlock
                {
                    Text = "No comments on this chapter yet. Drop your thoughts below!",
                    FontSize = 12,
                    Foreground = Application.Current.Resources.TryGetValue("FgSubtleBrush", out var sB) ? (Brush)sB : null,
                    Margin = new Thickness(4)
                });
                return;
            }

            foreach (var c in comments)
            {
                DrawerCommentsList.Children.Add(BuildDrawerCommentCard(c));
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("ReaderPage.LoadChapterCommentsAsync", ex);
        }
    }

    private UIElement BuildDrawerCommentCard(MangaDl.Core.Community.CommentItem c)
    {
        var card = new Border
        {
            Background = Application.Current.Resources.TryGetValue("SurfaceBrush", out var sB) ? (Brush)sB : null,
            BorderBrush = Application.Current.Resources.TryGetValue("DividerBrush", out var dB) ? (Brush)dB : null,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8)
        };

        var stack = new StackPanel { Spacing = 6 };

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var authorStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var nameText = new TextBlock { Text = c.Username, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 12 };
        var rankBadge = new Border
        {
            Background = Application.Current.Resources.TryGetValue("AccentMutedBrush", out var aM) ? (Brush)aM : null,
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 1, 4, 1),
            Child = new TextBlock
            {
                Text = c.HunterRank,
                FontSize = 10,
                Foreground = Application.Current.Resources.TryGetValue("AccentLightBrush", out var aL) ? (Brush)aL : null
            }
        };
        authorStack.Children.Add(nameText);
        authorStack.Children.Add(rankBadge);
        Grid.SetColumn(authorStack, 0);

        var timeDiff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - c.CreatedAt;
        var hoursAgo = Math.Max(1, (int)(timeDiff / (1000 * 3600)));
        var timeText = new TextBlock
        {
            Text = hoursAgo < 24 ? $"{hoursAgo}h" : $"{hoursAgo / 24}d",
            FontSize = 10,
            Foreground = Application.Current.Resources.TryGetValue("FgSubtleBrush", out var sub) ? (Brush)sub : null,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(timeText, 1);

        headerGrid.Children.Add(authorStack);
        headerGrid.Children.Add(timeText);
        stack.Children.Add(headerGrid);

        var body = new TextBlock { Text = c.Content, FontSize = 12, TextWrapping = TextWrapping.Wrap, LineHeight = 16 };
        stack.Children.Add(body);

        var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(0, 2, 0, 0) };
        var likeBtn = new Button
        {
            Style = Application.Current.Resources.TryGetValue("GhostButton", out var gB) ? (Style)gB : null,
            Padding = new Thickness(6, 2, 6, 2),
            Height = 24,
            FontSize = 10
        };
        var likeContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var likeIcon = new Controls.MdIcon { Kind = c.IsLiked ? "HeartFilled" : "Heart", Size = 11, Foreground = Application.Current.Resources.TryGetValue("AccentLightBrush", out var hB) ? (Brush)hB : null };
        var likeCount = new TextBlock { Text = c.Likes.ToString(), FontSize = 10, VerticalAlignment = VerticalAlignment.Center };
        likeContent.Children.Add(likeIcon);
        likeContent.Children.Add(likeCount);
        likeBtn.Content = likeContent;

        var currentLiked = c.IsLiked;
        var currentLikes = c.Likes;

        likeBtn.Click += async (_, _) =>
        {
            var nowLiked = await AppServices.Community.LikeCommentAsync(AppServices.Database, c.Id);
            currentLiked = nowLiked;
            currentLikes = nowLiked ? currentLikes + 1 : Math.Max(0, currentLikes - 1);
            likeIcon.Kind = currentLiked ? "HeartFilled" : "Heart";
            likeCount.Text = currentLikes.ToString();
        };

        footer.Children.Add(likeBtn);
        stack.Children.Add(footer);

        card.Content = stack;
        return card;
    }

    private async void OnPostDrawerCommentClicked(object sender, RoutedEventArgs e)
    {
        if (DrawerCommentInput == null) return;
        var text = DrawerCommentInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        var userStats = await AppServices.Database.GetAllCompletedProgressAsync();
        var userRank = GamificationService.GetHunterRank(userStats.Count * 10);
        var username = AppServices.Settings.UserName ?? "Hunter";
        var chId = !string.IsNullOrEmpty(_chapterId) ? _chapterId : _chapterNumber.ToString();

        await AppServices.Community.PostCommentAsync(
            AppServices.Database,
            _manga.Source,
            _manga.Id,
            chapterId: chId,
            username: username,
            userAvatar: null,
            hunterRank: userRank.Name,
            content: text);

        DrawerCommentInput.Text = string.Empty;
        await LoadChapterCommentsAsync();
        Nav.Toast("Comment posted!");
    }
}
