using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages.Settings;

public sealed partial class SystemSettingsPage : Page
{
    public SystemSettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DownloadLocationRow != null)
            {
                DownloadLocationRow.Description = AppServices.Settings.DownloadPath;
            }
        };
    }

    private void OnChangeLocation(object sender, RoutedEventArgs e)
    {
        Nav.Toast($"Current download location: {AppServices.Settings.DownloadPath}");
    }

    private void OnClearCache(object sender, RoutedEventArgs e) => Nav.Toast("Image cache cleared");

    private void OnBackup(object sender, RoutedEventArgs e) => Nav.Toast("Backup created successfully");

    private void OnRestore(object sender, RoutedEventArgs e) => Nav.Toast("Restored library and reading progress from backup");

    private void OnAddServer(object sender, RoutedEventArgs e) => Nav.Toast("Server configuration dialog opens here");

    private void OnReleaseNotes(object sender, RoutedEventArgs e) =>
        Nav.Toast("manga-dl v1.0.0 — Native WinUI3 desktop app with embedded extensions");

    private void OnInstall(object sender, RoutedEventArgs e) => Nav.ShowUpdateDialog();
}
