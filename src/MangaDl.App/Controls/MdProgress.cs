using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace MangaDl.Controls;

/// <summary>
/// Thin rounded progress bar (reading progress, downloads, goals, stat bars).
/// Value is 0–1. Height sets the thickness.
/// </summary>
public sealed partial class MdProgress : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(MdProgress), new PropertyMetadata(0.0, (d, _) => ((MdProgress)d).Layout()));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(MdProgress), new PropertyMetadata(null, (d, e) => ((MdProgress)d)._fill.Background = (Brush)e.NewValue));

    public static readonly DependencyProperty TrackProperty = DependencyProperty.Register(
        nameof(Track), typeof(Brush), typeof(MdProgress), new PropertyMetadata(null, (d, e) => ((MdProgress)d)._track.Background = (Brush)e.NewValue));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public Brush Fill
    {
        get => (Brush)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public Brush Track
    {
        get => (Brush)GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    private readonly Border _track = new();
    private readonly Border _fill = new() { HorizontalAlignment = HorizontalAlignment.Left };

    public MdProgress()
    {
        IsTabStop = false;
        Height = 4;
        _track.Background = (Brush)Application.Current.Resources["TrackBrush"];
        _fill.Background = (Brush)Application.Current.Resources["AccentBrush"];
        var grid = new Grid();
        grid.Children.Add(_track);
        grid.Children.Add(_fill);
        Content = grid;
        SizeChanged += (_, _) => Layout();
    }

    private void Layout()
    {
        var radius = new CornerRadius(ActualHeight / 2);
        _track.CornerRadius = radius;
        _fill.CornerRadius = radius;
        _fill.Width = Math.Max(0, Math.Min(1, Value)) * ActualWidth;
    }
}
