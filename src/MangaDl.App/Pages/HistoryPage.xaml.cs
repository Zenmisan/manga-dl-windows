using System.Collections.ObjectModel;
using MangaDl.Core;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages;

public sealed partial class HistoryPage : Page
{
    public ObservableCollection<HistoryItem> Items { get; } = new(Sample.History);

    public HistoryPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadHistoryAsync();
    }

    private async Task LoadHistoryAsync()
    {
        try
        {
            var records = await AppServices.Database.GetHistoryAsync(50);
            if (records.Count > 0)
            {
                Items.Clear();
                foreach (var r in records)
                {
                    var manga = new Manga(r.MangaId, r.MangaTitle, r.CoverUrl ?? "#1A2433", 0, false, false, r.Provider);
                    var dt = DateTimeOffset.FromUnixTimeMilliseconds(r.ReadAt);
                    var when = dt.Date == DateTimeOffset.UtcNow.Date ? $"Today · {dt:HH:mm}" : dt.ToString("MMM d");

                    Items.Add(new HistoryItem(manga, r.ChapterTitle ?? r.ChapterId, when));
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("HistoryPage.LoadHistoryAsync", ex);
            Nav.Toast("Couldn't load history");
        }
    }

    private void OnResume(object sender, RoutedEventArgs e)
    {
        var item = ((FrameworkElement)sender).DataContext as HistoryItem;
        Nav.Go(typeof(ReaderPage), item?.Manga);
    }
}
