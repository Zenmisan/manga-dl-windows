using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace MangaDl.Pages.Settings;

public sealed partial class ReaderSettingsPage : Page
{
    private bool _loading = true;

    public ReaderSettingsPage()
    {
        InitializeComponent();
        Loaded += OnPageLoaded;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        _loading = true;

        var s = AppServices.Settings;

        // Reading mode
        switch (s.ReaderMode)
        {
            case "Left to right":
                if (ModeLtr is not null) ModeLtr.IsChecked = true;
                break;
            case "Vertical":
                if (ModeVertical is not null) ModeVertical.IsChecked = true;
                break;
            case "Webtoon":
                if (ModeWebtoon is not null) ModeWebtoon.IsChecked = true;
                break;
            default:
                if (ModeRtl is not null) ModeRtl.IsChecked = true;
                break;
        }

        if (TwoPageSpreadToggle is not null) TwoPageSpreadToggle.IsChecked = s.TwoPageSpread;
        if (CropBordersToggle is not null) CropBordersToggle.IsChecked = s.CropBorders;

        if (PaddingSlider is not null)
        {
            PaddingSlider.Value = s.WebtoonSidePadding;
            if (PaddingValue is not null) PaddingValue.Text = $"{s.WebtoonSidePadding}%";
        }

        // Page fit
        switch (s.PageFit)
        {
            case "Fit width":
                if (FitWidth is not null) FitWidth.IsChecked = true;
                break;
            case "Original":
                if (FitOriginal is not null) FitOriginal.IsChecked = true;
                break;
            default:
                if (FitHeight is not null) FitHeight.IsChecked = true;
                break;
        }

        // Background
        switch (s.ReaderBackground)
        {
            case "Gray":
                if (BgGray is not null) BgGray.IsChecked = true;
                break;
            case "White":
                if (BgWhite is not null) BgWhite.IsChecked = true;
                break;
            default:
                if (BgBlack is not null) BgBlack.IsChecked = true;
                break;
        }

        if (ShowPageNumberToggle is not null) ShowPageNumberToggle.IsChecked = s.ShowPageNumber;
        if (OpenFullscreenToggle is not null) OpenFullscreenToggle.IsChecked = s.OpenFullscreen;
        if (ClickZonesToggle is not null) ClickZonesToggle.IsChecked = s.ClickZones;

        // Mouse wheel
        switch (s.MouseWheelAction)
        {
            case "Turn page":
                if (WheelTurnPage is not null) WheelTurnPage.IsChecked = true;
                break;
            case "Zoom":
                if (WheelZoom is not null) WheelZoom.IsChecked = true;
                break;
            default:
                if (WheelScroll is not null) WheelScroll.IsChecked = true;
                break;
        }

        // Filters
        if (BrightnessSlider is not null) BrightnessSlider.Value = s.ImageBrightness;
        if (GrayscaleToggle is not null) GrayscaleToggle.IsChecked = s.ImageGrayscale;
        if (InvertToggle is not null) InvertToggle.IsChecked = s.ImageInvert;
        if (SharpenToggle is not null) SharpenToggle.IsChecked = s.SharpenImages;

        _loading = false;
    }

    private void OnReadingModeChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (ModeLtr?.IsChecked == true)
        {
            AppServices.Settings.ReaderMode = "Left to right";
            AppServices.Settings.ReadingDirection = "LTR";
        }
        else if (ModeVertical?.IsChecked == true)
        {
            AppServices.Settings.ReaderMode = "Vertical";
        }
        else if (ModeWebtoon?.IsChecked == true)
        {
            AppServices.Settings.ReaderMode = "Webtoon";
        }
        else
        {
            AppServices.Settings.ReaderMode = "Right to left";
            AppServices.Settings.ReadingDirection = "RTL";
        }
        AppServices.Settings.Save();
    }

    private void OnTwoPageSpreadToggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        AppServices.Settings.TwoPageSpread = TwoPageSpreadToggle?.IsChecked == true;
        AppServices.Settings.Save();
    }

    private void OnCropBordersToggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        AppServices.Settings.CropBorders = CropBordersToggle?.IsChecked == true;
        AppServices.Settings.Save();
    }

    private void OnPaddingChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (PaddingValue is not null) PaddingValue.Text = $"{e.NewValue:0}%";
        if (_loading) return;
        AppServices.Settings.WebtoonSidePadding = (int)e.NewValue;
        AppServices.Settings.Save();
    }

    private void OnPageFitChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (FitWidth?.IsChecked == true)
            AppServices.Settings.PageFit = "Fit width";
        else if (FitOriginal?.IsChecked == true)
            AppServices.Settings.PageFit = "Original";
        else
            AppServices.Settings.PageFit = "Fit height";
        AppServices.Settings.Save();
    }

    private void OnBackgroundChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (BgGray?.IsChecked == true)
            AppServices.Settings.ReaderBackground = "Gray";
        else if (BgWhite?.IsChecked == true)
            AppServices.Settings.ReaderBackground = "White";
        else
            AppServices.Settings.ReaderBackground = "Black";
        AppServices.Settings.Save();
    }

    private void OnShowPageNumberToggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        AppServices.Settings.ShowPageNumber = ShowPageNumberToggle?.IsChecked == true;
        AppServices.Settings.Save();
    }

    private void OnOpenFullscreenToggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        AppServices.Settings.OpenFullscreen = OpenFullscreenToggle?.IsChecked == true;
        AppServices.Settings.Save();
    }

    private void OnClickZonesToggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        AppServices.Settings.ClickZones = ClickZonesToggle?.IsChecked == true;
        AppServices.Settings.Save();
    }

    private void OnMouseWheelChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (WheelTurnPage?.IsChecked == true)
            AppServices.Settings.MouseWheelAction = "Turn page";
        else if (WheelZoom?.IsChecked == true)
            AppServices.Settings.MouseWheelAction = "Zoom";
        else
            AppServices.Settings.MouseWheelAction = "Scroll";
        AppServices.Settings.Save();
    }

    private void OnBrightnessChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_loading) return;
        AppServices.Settings.ImageBrightness = (int)e.NewValue;
        AppServices.Settings.Save();
    }

    private void OnGrayscaleToggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        AppServices.Settings.ImageGrayscale = GrayscaleToggle?.IsChecked == true;
        AppServices.Settings.Save();
    }

    private void OnInvertToggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        AppServices.Settings.ImageInvert = InvertToggle?.IsChecked == true;
        AppServices.Settings.Save();
    }

    private void OnSharpenToggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        AppServices.Settings.SharpenImages = SharpenToggle?.IsChecked == true;
        AppServices.Settings.Save();
    }

    private void OnCustomiseShortcuts(object sender, RoutedEventArgs e) =>
        Nav.Toast("Keyboard shortcuts: Left/Right = page turn, F = fullscreen, Esc = exit reader");
}
