using MangaDl.Helpers;
using MangaDl.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;

namespace MangaDl.Pages;

/// <summary>
/// Manga reader. Pass "single" as the navigation parameter to open in single-page
/// layout; otherwise it opens as a two-page spread with the settings panel.
/// </summary>
public sealed partial class ReaderPage : Page
{
    private const int PageTotal = 38;
    private int _page = 14;
    private bool _spread = true;
    private bool _bookmarked;
    private bool _syncing;

    private Manga _currentManga = Sample.HollowCrown;

    public ReaderPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Nav.SetTitle($"{_currentManga.Title} — Ch. 48");
            Focus(FocusState.Programmatic);
        };
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is Manga m)
        {
            _currentManga = m;
        }
        else if (e.Parameter as string == "single")
        {
            SetSpread(false);
            SettingsToggle.IsChecked = false;
        }
    }

    // ----- Layout -----
    private void OnLayoutChecked(object sender, RoutedEventArgs e) => SetSpread(sender == SpreadOption);
    private void OnDualToggled(object sender, RoutedEventArgs e) => SetSpread(((ToggleButton)sender).IsChecked == true);

    private void SetSpread(bool spread)
    {
        if (_syncing || SpreadView is null || SingleView is null || DualToggle is null || SpreadOption is null || SingleOption is null) return;
        _syncing = true;
        _spread = spread;
        SpreadView.Visibility = X.Vis(spread);
        SingleView.Visibility = X.Vis(!spread);
        SpreadOption.IsChecked = spread;
        SingleOption.IsChecked = !spread;
        DualToggle.IsChecked = spread;
        _syncing = false;
        ShowPage();
    }

    // ----- Paging (placeholder pages; swap in real images) -----
    private void ShowPage()
    {
        if (PageCount is null || SliderPage is null || SinglePage is null || LeftPage is null || RightPage is null || PageSlider is null) return;
        PageCount.Text = _spread ? $"{_page}–{Math.Min(_page + 1, PageTotal)} / {PageTotal}" : $"{_page} / {PageTotal}";
        SliderPage.Text = _page.ToString();
        SinglePage.Label = $"[Page {_page}]";
        RightPage.Label = $"[Page {_page}]";
        LeftPage.Label = $"[Page {Math.Min(_page + 1, PageTotal)}]";
        if ((int)PageSlider.Value != _page) PageSlider.Value = _page;

        try
        {
            _ = AppServices.Database.SaveProgressAsync(_currentManga.Source, _currentManga.Id, "48", 48.0, _page, _page >= PageTotal)
                .ContinueWith(t => AppLog.Warn("ReaderPage.SaveProgressAsync", t.Exception!), TaskContinuationOptions.OnlyOnFaulted);
            _ = AppServices.Database.RecordHistoryAsync(_currentManga.Source, _currentManga.Id, _currentManga.Title, "48", $"Ch. 48 · page {_page} of {PageTotal}", _currentManga.Cover)
                .ContinueWith(t => AppLog.Warn("ReaderPage.RecordHistoryAsync", t.Exception!), TaskContinuationOptions.OnlyOnFaulted);
        }
        catch (Exception ex)
        {
            AppLog.Warn("ReaderPage.ShowPage", ex);
        }
    }

    private void Go(int delta)
    {
        _page = Math.Clamp(_page + delta * (_spread ? 2 : 1), 1, PageTotal);
        ShowPage();
    }

    private void OnPrevPage(object sender, RoutedEventArgs e) => Go(-1);
    private void OnNextPage(object sender, RoutedEventArgs e) => Go(1);

    private void OnSliderChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        _page = (int)e.NewValue;
        ShowPage();
    }

    /// <summary>Arrow keys and Space turn pages; F toggles fullscreen; Escape closes.</summary>
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

    // ----- Filters (labels only; apply to the page image when wiring) -----
    private void OnBrightness(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (BrightnessText is not null) BrightnessText.Text = $"{(int)e.NewValue}%";
    }

    private void OnContrast(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (ContrastText is not null) ContrastText.Text = $"{(int)e.NewValue}%";
    }
}
