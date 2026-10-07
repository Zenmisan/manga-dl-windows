using MangaDl.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace MangaDl.Services;

/// <summary>Accent options from Settings › General. Red is the brand default.</summary>
public sealed record Accent(string Name, string Main, string Light, string Soft);

public static class ThemeService
{
    public static readonly IReadOnlyList<Accent> Accents =
    [
        new("Red", "#DC2626", "#EF4444", "#FCA5A5"),
        new("Blue", "#2563EB", "#3B82F6", "#93C5FD"),
        new("Purple", "#7C3AED", "#8B5CF6", "#C4B5FD"),
        new("Green", "#16A34A", "#22C55E", "#86EFAC"),
        new("Orange", "#EA580C", "#F97316", "#FDBA74"),
        new("Pink", "#DB2777", "#EC4899", "#F9A8D4"),
    ];

    public static Accent Current { get; private set; } = Accents[0];

    /// <summary>
    /// Recolours the shared accent brushes in place, so every page using them
    /// updates immediately. Persist the choice (ApplicationData / settings file) when wiring settings.
    /// </summary>
    public static void SetAccent(Accent accent)
    {
        Current = accent;
        var main = X.ToColor(accent.Main);
        Set("AccentBrush", main);
        Set("AccentLightBrush", X.ToColor(accent.Light));
        Set("AccentSoftBrush", X.ToColor(accent.Soft));
        Set("AccentMutedBrush", WithAlpha(main, 0x29));
        Set("AccentFaintBrush", WithAlpha(main, 0x14));
        Set("AccentWashBrush", WithAlpha(main, 0x0D));
        Set("AccentNavBrush", WithAlpha(main, 0x2E));

        try
        {
            AppServices.Settings.AccentColor = accent.Name;
            AppServices.Settings.Save();
        }
        catch (Exception ex)
        {
            AppLog.Warn("ThemeService.SetAccent", ex);
        }
    }

    private static Color WithAlpha(Color c, byte a) => Microsoft.UI.ColorHelper.FromArgb(a, c.R, c.G, c.B);

    private static void Set(string key, Color color)
    {
        if (Application.Current.Resources[key] is SolidColorBrush brush) brush.Color = color;
    }
}
