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
}
