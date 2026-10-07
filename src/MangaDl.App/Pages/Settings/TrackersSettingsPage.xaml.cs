using MangaDl.Core;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages.Settings;

public sealed partial class TrackersSettingsPage : Page
{
    public IReadOnlyList<Tracker> Trackers => Sample.Trackers;

    public TrackersSettingsPage() => InitializeComponent();
}
