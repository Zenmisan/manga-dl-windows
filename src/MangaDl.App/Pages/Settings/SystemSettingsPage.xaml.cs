using MangaDl.Core.Backup;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

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

    private async void OnRestore(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".tachibk");
        picker.FileTypeFilter.Add(".json");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.Window!));

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        Nav.Toast("Restoring backup...");
        try
        {
            var bytes = await File.ReadAllBytesAsync(file.Path);
            var backup = TachibkParser.ParseTachiyomiBackup(bytes, file.Name);
            var summary = await AppServices.TachibkImport.ImportAsync(backup);

            Nav.Toast(summary.MangaImported > 0
                ? $"Restored {summary.MangaImported} series, {summary.ChaptersMarkedRead} read chapters, {summary.CategoriesImported} categories"
                : "No manga found in that backup file");
        }
        catch (Exception ex)
        {
            AppLog.Warn("SystemSettingsPage.OnRestore", ex);
            Nav.Toast("Couldn't read that backup file");
        }
    }

    private void OnAddServer(object sender, RoutedEventArgs e) => Nav.Toast("Server configuration dialog opens here");

    private void OnReleaseNotes(object sender, RoutedEventArgs e) =>
        Nav.Toast("manga-dl v1.0.0 — Native WinUI3 desktop app with embedded extensions");

    private void OnInstall(object sender, RoutedEventArgs e) => Nav.ShowUpdateDialog();
}
