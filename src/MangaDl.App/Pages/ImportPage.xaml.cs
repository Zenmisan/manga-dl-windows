using System.Collections.ObjectModel;
using MangaDl.Core;
using MangaDl.Core.Database.Entities;
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
        LibraryEntity? target = null;
        if (ExistingSeriesOption.IsChecked == true)
        {
            target = await PickExistingSeriesAsync();
            if (target is null) return; // user cancelled, or library is empty (toast already shown)
        }

        var importedSeries = 0;
        var importedChapters = 0;
        var failed = 0;

        foreach (var item in items)
        {
            if (item is StorageFile file)
            {
                var results = await AppServices.LocalImport.ImportStandaloneFileAsync(file.Path, target);
                foreach (var result in results)
                {
                    AddRow(result);
                    if (result.Status == LocalImportStatus.Error) failed++;
                }
                var done = results.Count(r => r.Status == LocalImportStatus.Done);
                if (done > 0) { importedSeries++; importedChapters += done; }
            }
            else if (item is StorageFolder folder)
            {
                var results = await AppServices.LocalImport.ImportFolderAsync(folder.Path, target);
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

    /// <summary>Lets the user pick which library series the import should add chapters
    /// to. Built in code rather than XAML — this is the only page needing it.</summary>
    private async Task<LibraryEntity?> PickExistingSeriesAsync()
    {
        var library = await AppServices.Database.GetLibraryAsync();
        if (library.Count == 0)
        {
            Nav.Toast("Library is empty — nothing to add to yet");
            return null;
        }

        var list = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            ItemsSource = library,
            DisplayMemberPath = "Title",
            MaxHeight = 400
        };

        var dialog = new ContentDialog
        {
            Title = "Add to which series?",
            Content = list,
            PrimaryButtonText = "Add here",
            CloseButtonText = "Cancel",
            IsPrimaryButtonEnabled = false,
            XamlRoot = XamlRoot
        };
        list.SelectionChanged += (_, _) => dialog.IsPrimaryButtonEnabled = list.SelectedItem is not null;

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? list.SelectedItem as LibraryEntity : null;
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
