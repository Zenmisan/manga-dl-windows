using MangaDl.Core;
using MangaDl.Helpers;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace MangaDl.Pages;

public sealed partial class ImportPage : Page
{
    public IReadOnlyList<ImportFile> Files { get; } = Sample.ImportQueue;

    public ImportPage() => InitializeComponent();

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "Import";
        DropZone.Background = X.Res("AccentMutedBrush");
    }

    private void OnDragLeave(object sender, DragEventArgs e) => DropZone.Background = X.Res("AccentWashBrush");

    // TODO(backend): hand the dropped StorageItems to the importer (e.Data.GetView().GetStorageItemsAsync()).
    private void OnDrop(object sender, DragEventArgs e)
    {
        DropZone.Background = X.Res("AccentWashBrush");
        Nav.Toast("Importing dropped files");
    }

    // TODO(backend): use Windows.Storage.Pickers.FileOpenPicker / FolderPicker
    // (initialise with WinRT.Interop.InitializeWithWindow and the main window handle).
    private void OnChooseFiles(object sender, RoutedEventArgs e) => Nav.Toast("File picker opens here");
    private void OnChooseFolder(object sender, RoutedEventArgs e) => Nav.Toast("Folder picker opens here");
}
