using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages.Settings;

public sealed partial class SystemSettingsPage : Page
{
    public SystemSettingsPage() => InitializeComponent();

    // TODO(backend): wire these to storage, backup and the updater.
    private void OnClearCache(object sender, RoutedEventArgs e) => Nav.Toast("Image cache cleared");

    private void OnBackup(object sender, RoutedEventArgs e) => Nav.Toast("Backup created");

    private void OnInstall(object sender, RoutedEventArgs e) => Nav.ShowUpdateDialog();
}
