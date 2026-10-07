using System.Collections.ObjectModel;
using MangaDl.Core;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages;

public sealed partial class SearchPage : Page
{
    public ObservableCollection<SearchGroup> Groups { get; } = new(Sample.SearchResults);

    public SearchPage() => InitializeComponent();

    private async void OnSearchSubmitted(object? sender, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return;

        SearchStatus.Text = $"Searching sources for \"{query}\"...";

        try
        {
            var results = await AppServices.Extensions.GlobalSearchAsync(query);
            if (results.Count > 0)
            {
                Groups.Clear();
                foreach (var g in results)
                {
                    Groups.Add(g);
                }
                SearchStatus.Text = $"{results.Sum(g => g.Results.Count)} results across {results.Count} sources";
            }
            else
            {
                SearchStatus.Text = "No results found";
            }
        }
        catch (Exception ex)
        {
            SearchStatus.Text = $"Search error: {ex.Message}";
        }
    }

    private void OnOpenManga(object sender, RoutedEventArgs e) =>
        Nav.Go(typeof(MangaDetailPage), ((FrameworkElement)sender).DataContext as Manga);

    private void OnSeeAll(object sender, RoutedEventArgs e) =>
        Nav.Go(typeof(BrowseSourcePage), (((FrameworkElement)sender).DataContext as SearchGroup)?.Source);
}
