using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages.Settings;

public sealed partial class GeneralSettingsPage : Page
{
    public IReadOnlyList<Accent> Accents => ThemeService.Accents;

    public GeneralSettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateRings();
    }

    private void OnAccent(object sender, RoutedEventArgs e)
    {
        var name = (string)((FrameworkElement)sender).Tag;
        ThemeService.SetAccent(Accents.First(a => a.Name == name));
        UpdateRings();
    }

    /// <summary>Draws the ring around the selected swatch.</summary>
    private void UpdateRings()
    {
        foreach (var button in FindButtons(this))
            button.BorderThickness = new Thickness((string)button.Tag == ThemeService.Current.Name ? 2 : 0);
    }

    private static IEnumerable<Button> FindButtons(DependencyObject root)
    {
        var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i);
            if (child is Button { Tag: string tag } b && ThemeService.Accents.Any(a => a.Name == tag)) yield return b;
            foreach (var nested in FindButtons(child)) yield return nested;
        }
    }

    // Ping connection test
    private async void OnTest(object sender, RoutedEventArgs e)
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var ping = await AppServices.Http.FetchAsync("https://api.mangadex.org/ping");
            sw.Stop();
            TestResult.Visibility = Visibility.Visible;
            LatencyText.Text = $"Connected · {sw.ElapsedMilliseconds} ms";
            Nav.Toast(ping.IsSuccess ? "Connection test successful" : "Ping completed");
        }
        catch
        {
            TestResult.Visibility = Visibility.Collapsed;
            Nav.Toast("Connection failed");
        }
    }
}
