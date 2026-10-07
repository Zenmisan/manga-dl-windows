using MangaDl.Core;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages;

public sealed partial class UpdatesPage : Page
{
    public IReadOnlyList<UpdateGroup> Groups { get; } = Sample.Updates;

    public UpdatesPage() => InitializeComponent();

    private void OnOpenManga(object sender, RoutedEventArgs e) =>
        Nav.Go(typeof(MangaDetailPage), (((FrameworkElement)sender).DataContext as UpdateItem)?.Manga);

    private void OnRead(object sender, RoutedEventArgs e) => Nav.Go(typeof(ReaderPage));

    private async void OnCheck(object sender, RoutedEventArgs e)
    {
        Nav.Toast("Checking library for new chapters");
        try
        {
            var lib = await AppServices.Database.GetLibraryAsync();
            Nav.Toast($"Checked {lib.Count} titles for updates");
        }
        catch (Exception ex)
        {
            AppLog.Warn("UpdatesPage.OnCheck", ex);
            Nav.Toast("Couldn't check for updates");
        }
    }
}
