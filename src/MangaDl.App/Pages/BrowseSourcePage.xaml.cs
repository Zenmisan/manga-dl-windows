using System.Collections.ObjectModel;
using MangaDl.Core;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace MangaDl.Pages;

/// <summary>Popular / Latest catalogue of one source. Navigation parameter: the source name.</summary>
public sealed partial class BrowseSourcePage : Page
{
    public ObservableCollection<Manga> Items { get; } = new(Sample.BrowseCatalog);

    private string _sourceName = "MangaDex";
    private string _sourceId = "mangadex";

    public BrowseSourcePage() => InitializeComponent();

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _sourceName = e.Parameter as string ?? "MangaDex";
        _sourceId = _sourceName.ToLowerInvariant().Replace(" ", "").Replace(".", "");

        TitleText.Text = _sourceName.ToUpperInvariant();
        SourceSearch.Placeholder = $"Search {_sourceName}";

        await LoadPopularAsync();
    }

    private async Task LoadPopularAsync()
    {
        try
        {
            var results = await AppServices.Extensions.GetPopularAsync(_sourceId, 1);
            if (results.Count > 0)
            {
                Items.Clear();
                foreach (var r in results)
                {
                    Items.Add(new Manga(
                        r.Id,
                        r.Title,
                        r.CoverUrl ?? "#1A2433",
                        0,
                        false,
                        false,
                        _sourceName));
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("BrowseSourcePage.LoadPopularAsync", ex);
            Nav.Toast($"Couldn't load {_sourceName}");
        }
    }

    private async void OnSearchSubmitted(object? sender, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            await LoadPopularAsync();
            return;
        }

        try
        {
            var results = await AppServices.Extensions.SearchAsync(_sourceId, query, 1);
            if (results.Count > 0)
            {
                Items.Clear();
                foreach (var r in results)
                {
                    Items.Add(new Manga(
                        r.Id,
                        r.Title,
                        r.CoverUrl ?? "#1A2433",
                        0,
                        false,
                        false,
                        _sourceName));
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("BrowseSourcePage.OnSearchSubmitted", ex);
            Nav.Toast($"Search failed on {_sourceName}");
        }
    }

    private void OnRemoveFilter(object sender, RoutedEventArgs e)
    {
        if (FindName((string)((FrameworkElement)sender).Tag) is UIElement chip) chip.Visibility = Visibility.Collapsed;
    }

    private void OnOpenManga(object sender, RoutedEventArgs e) =>
        Nav.Go(typeof(MangaDetailPage), ((FrameworkElement)sender).DataContext as Manga);

    private void OnBack(object sender, RoutedEventArgs e) => Nav.Back();
}
