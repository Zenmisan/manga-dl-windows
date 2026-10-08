using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Path = Microsoft.UI.Xaml.Shapes.Path; // disambiguates from System.IO.Path (ImplicitUsings)

namespace MangaDl.Controls;

/// <summary>
/// Stroke icon on a 24×24 grid (2px round strokes, the Lucide style) drawn from
/// the exact SVG paths used in the designs. Colour follows Foreground.
/// Usage: &lt;c:MdIcon Kind="Search" Size="20" /&gt;
/// </summary>
public sealed partial class MdIcon : UserControl
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(string), typeof(MdIcon), new PropertyMetadata("", (d, _) => ((MdIcon)d).Build()));

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(double), typeof(MdIcon), new PropertyMetadata(20.0, (d, _) => ((MdIcon)d).Build()));

    public string Kind
    {
        get => (string)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    private readonly List<Path> _paths = [];

    public MdIcon()
    {
        IsTabStop = false;
        RegisterPropertyChangedCallback(ForegroundProperty, (_, _) => Recolor());
    }

    private void Build()
    {
        _paths.Clear();
        if (!Icons.Map.TryGetValue(Kind ?? "", out var def))
        {
            Content = null;
            return;
        }

        var canvas = new Canvas { Width = 24, Height = 24 };
        foreach (var d in def.Paths)
        {
            var path = new Path
            {
                Data = (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), d),
                StrokeThickness = def.Stroke,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
            };
            _paths.Add(path);
            canvas.Children.Add(path);
        }

        Content = new Viewbox { Width = Size, Height = Size, Child = canvas };
        Recolor();
    }

    private void Recolor()
    {
        if (!Icons.Map.TryGetValue(Kind ?? "", out var def)) return;
        foreach (var p in _paths)
        {
            if (def.Filled)
            {
                p.Fill = Foreground;
                p.Stroke = null;
            }
            else
            {
                p.Stroke = Foreground;
                p.Fill = null;
            }
        }
    }
}

/// <summary>Icon definitions. Same paths as the Android app's MdIcons.</summary>
public static class Icons
{
    public sealed record Def(string[] Paths, double Stroke = 2, bool Filled = false);

    public static readonly Dictionary<string, Def> Map = new()
    {
        ["Search"] = new(["M11 4a7 7 0 1 0 0 14a7 7 0 1 0 0-14z", "M20 20l-3.5-3.5"]),
        ["Filter"] = new(["M4 6h16M7 12h10M10 18h4"]),
        ["More"] = new(["M12 5h.01M12 12h.01M12 19h.01"], 3),
        ["Library"] = new(["M5 4h11a3 3 0 0 1 3 3v13H8a3 3 0 0 1-3-3z", "M5 17a3 3 0 0 1 3-3h11"]),
        ["Updates"] = new(["M6 16v-5a6 6 0 0 1 12 0v5l2 2H4z", "M10 21h4"]),
        ["History"] = new(["M12 4a8 8 0 1 0 0 16a8 8 0 1 0 0-16z", "M12 8v4l3 2"]),
        ["Browse"] = new(["M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18z", "M15.5 8.5l-2 5-5 2 2-5z"]),
        ["Menu"] = new(["M4 7h16M4 12h16M4 17h16"]),
        ["Back"] = new(["M15 5l-7 7 7 7"]),
        ["ChevronRight"] = new(["M9 5l7 7-7 7"]),
        ["ChevronDown"] = new(["M6 9l6 6 6-6"]),
        ["ChevronUp"] = new(["M6 15l6-6 6 6"]),
        ["Download"] = new(["M12 4v11M7 10l5 5 5-5M5 20h14"]),
        ["Upload"] = new(["M4 20h16M12 16V4M7 9l5-5 5 5"]),
        ["Check"] = new(["M5 12l5 5 9-10"], 2.5),
        ["Play"] = new(["M8 5l11 7-11 7z"], Filled: true),
        ["Pause"] = new(["M8 5v14M16 5v14"], 2.2),
        ["Bookmark"] = new(["M7 4h10v16l-5-4-5 4z"]),
        ["BookmarkFilled"] = new(["M6 3h12v18l-6-4-6 4z"], Filled: true),
        ["Sliders"] = new(["M4 7h10M18 7h2M4 17h4M12 17h8", "M16 5a2 2 0 1 0 0 4a2 2 0 1 0 0-4z", "M10 15a2 2 0 1 0 0 4a2 2 0 1 0 0-4z"]),
        ["Settings"] = new(["M4 7h10M18 7h2M4 17h4M12 17h8M16 5v4M10 15v4"]),
        ["Globe"] = new(["M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18z", "M3 12h18M12 3a14 14 0 0 1 0 18M12 3a14 14 0 0 0 0 18"]),
        ["Sort"] = new(["M7 4v16M4 17l3 3 3-3M17 20V4M14 7l3-3 3 3"]),
        ["Close"] = new(["M6 6l12 12M18 6L6 18"]),
        ["Refresh"] = new(["M20 12a8 8 0 1 1-2.3-5.7M20 4v5h-5"]),
        ["Trash"] = new(["M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13"]),
        ["Pin"] = new(["M12 17v5M8 3h8l-1 6 3 4H6l3-4z"]),
        ["Mail"] = new(["M4 6h16v12H4zM4 7l8 6 8-6"]),
        ["Stats"] = new(["M5 20V10M12 20V4M19 20v-7"]),
        ["Backup"] = new(["M4 6h16v4H4zM6 10v10h12V10M10 14h4"]),
        ["Help"] = new(["M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18z", "M9.5 9.5a2.5 2.5 0 1 1 3.5 2.3c-.6.3-1 .9-1 1.6M12 17h.01"]),
        ["User"] = new(["M12 4a4 4 0 1 0 0 8a4 4 0 1 0 0-8z", "M4 20a8 8 0 0 1 16 0"]),
        ["Track"] = new(["M20 12a8 8 0 0 1-14 5.3M4 12a8 8 0 0 1 14-5.3", "M18 3v4h-4M6 21v-4h4"]),
        ["Share"] = new(["M12 4v12M7 9l5-5 5 5M5 14v6h14v-6"]),
        ["Warning"] = new(["M12 4l9 16H3z", "M12 10v4M12 17h.01"]),
        ["ArrowUp"] = new(["M12 20V6M6 12l6-6 6 6"]),
        ["Pages"] = new(["M6 3h12a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z", "M12 3v18"]),
        ["Crop"] = new(["M4 9V4h5M20 15v5h-5M4 4l6 6M20 20l-6-6"]),
        ["Sun"] = new(["M12 8a4 4 0 1 0 0 8a4 4 0 1 0 0-8z", "M12 2v2M12 20v2M2 12h2M20 12h2M5 5l1.5 1.5M17.5 17.5L19 19M5 19l1.5-1.5M17.5 6.5L19 5"]),
        ["PrevChapter"] = new(["M18 6l-6 6 6 6M8 6v12"]),
        ["NextChapter"] = new(["M6 6l6 6-6 6M16 6v12"]),
        ["List"] = new(["M9 6h11M9 12h11M9 18h11M4 6h.01M4 12h.01M4 18h.01"]),
        ["CloudCheck"] = new(["M7 18a5 5 0 0 1-.5-10A6 6 0 0 1 18 9a4.5 4.5 0 0 1-.5 9z", "M9 13l2 2 4-4"]),
        ["Edit"] = new(["M4 20h4L19 9l-4-4L4 16zM14 6l4 4"]),
        ["Plus"] = new(["M12 5v14M5 12h14"]),
        ["Fullscreen"] = new(["M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5"]),
        ["Folder"] = new(["M3 6a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"]),
        ["Grip"] = new(["M9 6h.01M15 6h.01M9 12h.01M15 12h.01M9 18h.01M15 18h.01"], 3),
    };
}
