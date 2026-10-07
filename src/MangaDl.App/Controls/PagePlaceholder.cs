using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace MangaDl.Controls;

/// <summary>
/// Stand-in for a manga page (paper with panel outlines) until real images load.
/// Replace with an Image bound to the page bitmap when wiring the reader.
/// </summary>
public sealed partial class PagePlaceholder : UserControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(PagePlaceholder), new PropertyMetadata("", (d, e) => ((PagePlaceholder)d)._label.Text = (string)e.NewValue));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    private readonly TextBlock _label = new()
    {
        FontSize = 13,
        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public PagePlaceholder()
    {
        IsTabStop = false;
        var res = Application.Current.Resources;
        _label.Foreground = (Brush)res["PaperTextBrush"];
        var ink = (Brush)res["PaperInkBrush"];

        var grid = new Grid
        {
            Background = (Brush)res["PaperBrush"],
            Padding = new Thickness(18),
            RowSpacing = 10,
            ColumnSpacing = 10,
        };
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        Border Panel(string hex, int row, int col, int span)
        {
            var b = new Border { Background = Helpers.X.Hex(hex), BorderBrush = ink, BorderThickness = new Thickness(2) };
            Grid.SetRow(b, row);
            Grid.SetColumn(b, col);
            Grid.SetColumnSpan(b, span);
            grid.Children.Add(b);
            return b;
        }

        Panel("#D4D0C6", 0, 0, 2).Child = _label;
        Panel("#DCD8CF", 1, 0, 1);
        Panel("#CFCAC0", 1, 1, 1);
        Panel("#D8D4CA", 2, 0, 2);
        Content = grid;
    }
}
