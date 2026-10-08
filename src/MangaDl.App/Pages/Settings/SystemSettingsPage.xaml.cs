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
            UpdateStorageMetrics();
        };
    }

    private void UpdateStorageMetrics()
    {
        var path = AppServices.Settings.DownloadPath;
        try
        {
            var root = Path.GetPathRoot(path);
            if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
            {
                var drive = new DriveInfo(root);
                var totalBytes = drive.TotalSize;
                var freeBytes = drive.AvailableFreeSpace;
                var usedBytes = Math.Max(0, totalBytes - freeBytes);
                var usedGb = usedBytes / (1024.0 * 1024.0 * 1024.0);
                var totalGb = totalBytes / (1024.0 * 1024.0 * 1024.0);

                if (StorageUsedText != null) StorageUsedText.Text = $"{usedGb:F1} GB used";
                if (StorageFreeText != null) StorageFreeText.Text = $"of {totalGb:F1} GB total";
                if (StorageProgressBar != null && totalBytes > 0)
                    StorageProgressBar.Value = Math.Clamp((double)usedBytes / totalBytes, 0.0, 1.0);
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("SystemSettingsPage.DriveInfo", ex);
        }

        // Asynchronously calculate downloads and cache directory sizes
        _ = Task.Run(() =>
        {
            long downloadBytes = 0;
            if (Directory.Exists(path))
            {
                try
                {
                    downloadBytes = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                        .Sum(f => new FileInfo(f).Length);
                }
                catch { }
            }

            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "manga-dl");
            long cacheBytes = 0;
            if (Directory.Exists(appDataDir))
            {
                try
                {
                    cacheBytes = Directory.EnumerateFiles(appDataDir, "*", SearchOption.AllDirectories)
                        .Sum(f => new FileInfo(f).Length);
                }
                catch { }
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                if (DownloadsStorageText != null)
                    DownloadsStorageText.Text = $"Downloads {FormatBytes(downloadBytes)}";
                if (CacheStorageText != null)
                    CacheStorageText.Text = $"Cache {FormatBytes(cacheBytes)}";
            });
        });
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024)
            return $"{(bytes / (1024.0 * 1024 * 1024)):F1} GB";
        if (bytes >= 1024L * 1024)
            return $"{(bytes / (1024.0 * 1024)):F1} MB";
        if (bytes >= 1024L)
            return $"{(bytes / 1024.0):F0} KB";
        return $"{bytes} B";
    }

    private async void OnChangeLocation(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.Window!));

            var folder = await picker.PickSingleFolderAsync();
            if (folder is null) return;

            AppServices.Settings.DownloadPath = folder.Path;
            AppServices.Settings.Save();

            if (DownloadLocationRow != null)
            {
                DownloadLocationRow.Description = folder.Path;
            }

            UpdateStorageMetrics();
            Nav.Toast($"Download location updated to {folder.Path}");
        }
        catch (Exception ex)
        {
            AppLog.Warn("SystemSettingsPage.OnChangeLocation", ex);
            Nav.Toast("Couldn't change download location");
        }
    }

    private void OnClearCache(object sender, RoutedEventArgs e) => Nav.Toast("Image cache cleared");

    private async void OnBackup(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileSavePicker();
            picker.SuggestedStartLocation = PickerLocationId.Downloads;
            picker.FileTypeChoices.Add("Manga-DL Backup", new List<string> { ".mangadl", ".json" });
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            picker.SuggestedFileName = $"manga-dl-backup-{timestamp}.mangadl";
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.Window!));

            var file = await picker.PickSaveFileAsync();
            if (file is null) return;

            Nav.Toast("Creating backup...");
            var bytes = await AppServices.Backup.ExportBackupBytesAsync();

            using (var stream = await file.OpenStreamForWriteAsync())
            {
                stream.SetLength(0);
                await stream.WriteAsync(bytes, 0, bytes.Length);
                await stream.FlushAsync();
            }

            Nav.Toast($"Backup saved to {file.Name}");
        }
        catch (Exception ex)
        {
            AppLog.Warn("SystemSettingsPage.OnBackup", ex);
            Nav.Toast("Couldn't create backup file");
        }
    }

    private async void OnRestore(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".mangadl");
        picker.FileTypeFilter.Add(".tachibk");
        picker.FileTypeFilter.Add(".json");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.Window!));

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        Nav.Toast("Restoring backup...");
        try
        {
            // StorageFile stream APIs, not File.ReadAllBytesAsync(file.Path) — in a packaged
            // (MSIX) build the picked file can be a virtualized/broker path that plain
            // System.IO can't open directly.
            byte[] bytes;
            using (var stream = await file.OpenStreamForReadAsync())
            using (var ms = new MemoryStream())
            {
                await stream.CopyToAsync(ms);
                bytes = ms.ToArray();
            }

            if (MangadlBackupService.IsMangadlBackup(bytes))
            {
                var summary = await AppServices.Backup.RestoreBackupAsync(bytes);
                Nav.Toast(summary.MangaRestored > 0
                    ? $"Restored {summary.MangaRestored} series, {summary.ProgressRestored} chapters, {summary.CategoriesRestored} categories, {summary.TrackerBindsRestored} trackers"
                    : "No manga found in that backup file");
            }
            else
            {
                var backup = TachibkParser.ParseTachiyomiBackup(bytes, file.Name);
                var summary = await AppServices.TachibkImport.ImportAsync(backup);

                Nav.Toast(summary.MangaImported > 0
                    ? $"Restored {summary.MangaImported} series, {summary.ChaptersMarkedRead} read chapters, {summary.CategoriesImported} categories"
                    : "No manga found in that backup file");
            }
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
