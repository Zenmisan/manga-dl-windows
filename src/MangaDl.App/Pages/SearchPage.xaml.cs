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
        SearchProgressSkeleton.Visibility = Visibility.Visible;

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
        finally
        {
            SearchProgressSkeleton.Visibility = Visibility.Collapsed;
        }
    }

    private void OnResetFilters(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = "";
        SearchStatus.Text = "Filters reset";
        Groups.Clear();
        foreach (var g in Sample.SearchResults) Groups.Add(g);
    }

    private void OnApplyFilters(object sender, RoutedEventArgs e)
    {
        var text = SearchBox.Text?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            OnSearchSubmitted(this, text);
        }
        else
        {
            Nav.Toast("Filters applied");
        }
    }

    private void OnOpenManga(object sender, RoutedEventArgs e) =>
        Nav.Go(typeof(MangaDetailPage), ((FrameworkElement)sender).DataContext as Manga);

    private void OnSeeAll(object sender, RoutedEventArgs e) =>
        Nav.Go(typeof(BrowseSourcePage), (((FrameworkElement)sender).DataContext as SearchGroup)?.Source);
}
