using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace MangaDl.Pages.Settings;

/// <summary>Settings host: section nav on the left, the section page on the right.
/// Navigation parameter picks the section ("Account", "General", "Reader", "Library", "Trackers", "System").</summary>
public sealed partial class SettingsPage : Page
{
    private static readonly Dictionary<string, Type> Sections = new()
    {
        ["Account"] = typeof(AccountSettingsPage),
        ["General"] = typeof(GeneralSettingsPage),
        ["Reader"] = typeof(ReaderSettingsPage),
        ["Library"] = typeof(LibrarySettingsPage),
        ["Trackers"] = typeof(TrackersSettingsPage),
        ["System"] = typeof(SystemSettingsPage),
    };

    public SettingsPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Show(e.Parameter as string ?? "General");
    }

    private void OnSection(object sender, RoutedEventArgs e) => Show((string)((FrameworkElement)sender).Tag);

    private void Show(string key)
    {
        if (!Sections.TryGetValue(key, out var page)) page = typeof(GeneralSettingsPage);
        foreach (var item in new[] { NavAccount, NavGeneral, NavReader, NavLibrary, NavTrackers, NavSystem })
            item.IsChecked = (string)item.Tag == key;
        if (SectionFrame.SourcePageType != page)
            SectionFrame.Navigate(page, null, new SuppressNavigationTransitionInfo());
    }
}
