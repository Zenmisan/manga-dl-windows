using System.Collections.ObjectModel;
using MangaDl.Core;
using MangaDl.Core.Database.Entities;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages;

public sealed partial class LibraryPage : Page
{
    public ObservableCollection<Manga> Items { get; } = new(Sample.Library);
    public ObservableCollection<ContinueItem> Continue { get; } = new(Sample.ContinueReading);

    public LibraryPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadLibraryAsync();
    }

    private async Task LoadLibraryAsync()
    {
        try
        {
            var saved = await AppServices.Database.GetLibraryAsync();
            if (saved.Count > 0)
            {
                Items.Clear();
                foreach (var s in saved)
                {
                    Items.Add(new Manga(
                        s.MangaId,
                        s.Title,
                        s.CoverUrl ?? "#1A2433",
                        0,
                        false,
                        true,
                        s.Provider));
                }
            }

            var history = await AppServices.Database.GetHistoryAsync(5);
            if (history.Count > 0)
            {
                Continue.Clear();
                foreach (var h in history)
                {
                    var manga = Items.FirstOrDefault(m => m.Id == h.MangaId)
                        ?? new Manga(h.MangaId, h.MangaTitle, h.CoverUrl ?? "#1A2433", 0, false, true, h.Provider);
                    Continue.Add(new ContinueItem(manga, h.ChapterTitle ?? h.ChapterId, 0.5));
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("LibraryPage.LoadLibraryAsync", ex);
            Nav.Toast("Couldn't load library");
        }
    }

    private void OnOpenManga(object sender, RoutedEventArgs e) =>
        Nav.Go(typeof(MangaDetailPage), ((FrameworkElement)sender).DataContext ?? Sample.HollowCrown);

    private void OnResume(object sender, RoutedEventArgs e) => Nav.Go(typeof(ReaderPage));

    private void OnUpdateLibrary(object sender, RoutedEventArgs e) =>
        Nav.Toast($"Checking {Items.Count} manga for new chapters");
}
