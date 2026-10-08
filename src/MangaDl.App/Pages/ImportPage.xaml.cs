using System.Collections.ObjectModel;
using MangaDl.Core;
using MangaDl.Core.Local;
using MangaDl.Helpers;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace MangaDl.Pages;

public sealed partial class ImportPage : Page
{
    public ObservableCollection<ImportFile> Files { get; } = [];

    public ImportPage() => InitializeComponent();

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "Import";
        DropZone.Background = X.Res("AccentMutedBrush");
    }

    private void OnDragLeave(object sender, DragEventArgs e) => DropZone.Background = X.Res("AccentWashBrush");

    private async void OnDrop(object sender, DragEventArgs e)
    {
        DropZone.Background = X.Res("AccentWashBrush");
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;

        var items = await e.DataView.GetStorageItemsAsync();
        await ImportItemsAsync(items);
    }

    private async void OnChooseFiles(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".cbz");
        picker.FileTypeFilter.Add(".zip");
        picker.FileTypeFilter.Add(".epub");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.Window!));

        var files = await picker.PickMultipleFilesAsync();
        if (files.Count == 0) return;
        await ImportItemsAsync(files);
    }

    private async void OnChooseFolder(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.Window!));

        var folder = await picker.PickSingleFolderAsync();
        if (folder is null) return;
        await ImportItemsAsync([folder]);
    }

    private async Task ImportItemsAsync(IReadOnlyList<IStorageItem> items)
    {
        if (ExistingSeriesOption.IsChecked == true)
        {
            Nav.Toast("Adding to an existing series needs a picker that isn't built yet — pick \"New series per folder\" for now");
            return;
        }

        var importedSeries = 0;
        var importedChapters = 0;
        var failed = 0;

        foreach (var item in items)
        {
            if (item is StorageFile file)
            {
                var result = await AppServices.LocalImport.ImportStandaloneFileAsync(file.Path);
                AddRow(result);
                if (result.Status == LocalImportStatus.Done) { importedSeries++; importedChapters++; }
                else if (result.Status == LocalImportStatus.Error) failed++;
            }
            else if (item is StorageFolder folder)
            {
                var results = await AppServices.LocalImport.ImportFolderAsync(folder.Path);
                foreach (var result in results)
                {
                    AddRow(result);
                    if (result.Status == LocalImportStatus.Error) failed++;
                }
                var done = results.Count(r => r.Status == LocalImportStatus.Done);
                if (done > 0) { importedSeries++; importedChapters += done; }
            }
        }

        if (importedChapters > 0)
        {
            Nav.Toast(failed > 0
                ? $"Imported {importedChapters} chapter(s) across {importedSeries} series — {failed} failed"
                : $"Imported {importedChapters} chapter(s) across {importedSeries} series");
        }
        else if (failed > 0)
        {
            Nav.Toast("Import failed — check the file list below");
        }
    }

    private void AddRow(LocalImportResult result)
    {
        var (status, progress, kind) = result.Status switch
        {
            LocalImportStatus.Done => (result.Message, 1.0, "done"),
            LocalImportStatus.Error => (result.Message, 1.0, "error"),
            _ => (result.Message, 1.0, "error"),
        };
        Files.Insert(0, new ImportFile(result.Name, result.Ext, status, progress, kind));
    }

    private void OnGuide(object sender, RoutedEventArgs e) => Nav.Go(typeof(HelpPage));
}
