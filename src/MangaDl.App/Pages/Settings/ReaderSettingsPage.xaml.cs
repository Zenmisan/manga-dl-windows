using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace MangaDl.Pages.Settings;

public sealed partial class ReaderSettingsPage : Page
{
    public ReaderSettingsPage() => InitializeComponent();

    private void OnPaddingChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        // Fires during InitializeComponent, before PaddingValue exists.
        if (PaddingValue is not null) PaddingValue.Text = $"{e.NewValue:0}%";
    }

    private void OnCustomiseShortcuts(object sender, RoutedEventArgs e) =>
        Nav.Toast("Keyboard shortcuts: Left/Right = page turn, F = fullscreen, Esc = exit reader");
}
