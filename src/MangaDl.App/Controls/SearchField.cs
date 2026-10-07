using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace MangaDl.Controls;

/// <summary>Rounded search box with a leading magnifier, as in the designs.</summary>
public sealed partial class SearchField : UserControl
{
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder), typeof(string), typeof(SearchField), new PropertyMetadata("", (d, e) =>
        {
            var f = (SearchField)d;
            f._box.PlaceholderText = (string)e.NewValue;
            AutomationProperties.SetName(f._box, (string)e.NewValue);
        }));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(SearchField), new PropertyMetadata("", (d, e) =>
        {
            var f = (SearchField)d;
            if (f._box.Text != (string)e.NewValue) f._box.Text = (string)e.NewValue;
        }));

    /// <summary>Large variant (56px, accent border) used on the Search page.</summary>
    public static readonly DependencyProperty IsLargeProperty = DependencyProperty.Register(
        nameof(IsLarge), typeof(bool), typeof(SearchField), new PropertyMetadata(false, (d, _) => ((SearchField)d).ApplySize()));

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsLarge
    {
        get => (bool)GetValue(IsLargeProperty);
        set => SetValue(IsLargeProperty, value);
    }

    public event EventHandler<string>? Submitted;

    private readonly Border _frame = new();
    private readonly MdIcon _icon = new() { Kind = "Search", Size = 18, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBox _box = new()
    {
        BorderThickness = new Thickness(0),
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
        VerticalAlignment = VerticalAlignment.Center,
        Padding = new Thickness(0, 6, 0, 0),
        MinHeight = 0,
    };

    public SearchField()
    {
        var res = Application.Current.Resources;
        _icon.Foreground = (Brush)res["FgSubtleBrush"];
        _box.FontFamily = (FontFamily)res["InterFont"];
        _box.Resources["TextControlBackgroundPointerOver"] = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        _box.Resources["TextControlBackgroundFocused"] = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        _box.Resources["TextControlBorderThemeThicknessFocused"] = new Thickness(0);
        _box.TextChanged += (_, _) => Text = _box.Text;
        _box.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter) Submitted?.Invoke(this, _box.Text);
        };

        var grid = new Grid { ColumnSpacing = 10, Padding = new Thickness(14, 0, 14, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(_box, 1);
        grid.Children.Add(_icon);
        grid.Children.Add(_box);

        _frame.Child = grid;
        _frame.CornerRadius = new CornerRadius(12);
        _frame.BorderThickness = new Thickness(1);
        _frame.Background = (Brush)res["SurfaceRaisedBrush"];
        Content = _frame;
        ApplySize();
    }

    private void ApplySize()
    {
        var res = Application.Current.Resources;
        _frame.Height = IsLarge ? 56 : 42;
        _frame.CornerRadius = new CornerRadius(IsLarge ? 14 : 12);
        _frame.BorderBrush = (Brush)res[IsLarge ? "AccentBrush" : "TrackBrush"];
        _box.FontSize = IsLarge ? 18 : 14;
        _box.FontWeight = IsLarge ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        _icon.Size = IsLarge ? 22 : 18;
    }
}
