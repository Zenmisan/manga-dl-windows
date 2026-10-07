using System.Collections.ObjectModel;
using MangaDl.Core;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages.Settings;

public sealed partial class TrackersSettingsPage : Page
{
    public ObservableCollection<Tracker> Trackers { get; } = new(Sample.Trackers);

    public TrackersSettingsPage() => InitializeComponent();

    private void OnTrackerAction(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is Tracker tracker)
        {
            var idx = Trackers.IndexOf(tracker);
            if (idx >= 0)
            {
                var updated = tracker with { Connected = !tracker.Connected };
                Trackers[idx] = updated;
                Nav.Toast(updated.Connected ? $"Connected to {tracker.Name}" : $"Disconnected from {tracker.Name}");
            }
        }
    }
}
