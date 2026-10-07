using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;

namespace MangaDl.Controls;

/// <summary>
/// One settings row: label + optional description on the left, a control on the
/// right (the XAML content), and an optional full-width control underneath (Below).
/// Ends with a hairline, like the designs.
/// <code>
/// &lt;c:SettingRow Label="Launch on startup" Description="…"&gt;
///     &lt;ToggleButton Style="{StaticResource MdSwitch}" /&gt;
/// &lt;/c:SettingRow&gt;
/// </code>
/// </summary>
[ContentProperty(Name = nameof(Control))]
public sealed partial class SettingRow : UserControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(SettingRow), new PropertyMetadata("", (d, e) => ((SettingRow)d).Update()));

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(SettingRow), new PropertyMetadata("", (d, e) => ((SettingRow)d).Update()));

    public static readonly DependencyProperty ControlProperty = DependencyProperty.Register(
        nameof(Control), typeof(object), typeof(SettingRow), new PropertyMetadata(null, (d, e) => ((SettingRow)d).Update()));

    public static readonly DependencyProperty BelowProperty = DependencyProperty.Register(
        nameof(Below), typeof(object), typeof(SettingRow), new PropertyMetadata(null, (d, e) => ((SettingRow)d).Update()));

    public static readonly DependencyProperty ShowDividerProperty = DependencyProperty.Register(
        nameof(ShowDivider), typeof(bool), typeof(SettingRow), new PropertyMetadata(true, (d, e) => ((SettingRow)d).Update()));

    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public object? Control { get => GetValue(ControlProperty); set => SetValue(ControlProperty, value); }
    public object? Below { get => GetValue(BelowProperty); set => SetValue(BelowProperty, value); }
    public bool ShowDivider { get => (bool)GetValue(ShowDividerProperty); set => SetValue(ShowDividerProperty, value); }

    private readonly Grid _root = new() { MinHeight = 56, Padding = new Thickness(0, 6, 0, 6), RowSpacing = 10, ColumnSpacing = 14 };
    private readonly TextBlock _label = new() { FontSize = 14, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _description = new() { FontSize = 12, LineHeight = 17, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
    private readonly ContentPresenter _control = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly ContentPresenter _below = new();

    public SettingRow()
    {
        IsTabStop = false;
        var res = Application.Current.Resources;
        _label.Foreground = (Brush)res["FgBrush"];
        _description.Foreground = (Brush)res["FgSubtleBrush"];
        _root.BorderBrush = (Brush)res["DividerBrush"];

        _root.ColumnDefinitions.Add(new ColumnDefinition());
        _root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(_label);
        text.Children.Add(_description);
        Grid.SetColumn(_control, 1);
        Grid.SetRow(_below, 1);
        Grid.SetColumnSpan(_below, 2);
        _root.Children.Add(text);
        _root.Children.Add(_control);
        _root.Children.Add(_below);
        Content = _root;
        Update();
    }

    private void Update()
    {
        _label.Text = Label;
        _description.Text = Description;
        _description.Visibility = string.IsNullOrEmpty(Description) ? Visibility.Collapsed : Visibility.Visible;
        _control.Content = Control;
        _below.Content = Below;
        _below.Visibility = Below is null ? Visibility.Collapsed : Visibility.Visible;
        _root.BorderThickness = new Thickness(0, 0, 0, ShowDivider ? 1 : 0);

        // Give the control an accessible name from the label when it has none.
        foreach (var child in new[] { Control, Below })
        {
            if (child is DependencyObject element && string.IsNullOrEmpty(AutomationProperties.GetName(element)))
                AutomationProperties.SetName(element, Label);
        }
    }
}
