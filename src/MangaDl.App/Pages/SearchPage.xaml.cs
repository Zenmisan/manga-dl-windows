using System.Collections.ObjectModel;
using MangaDl.Core;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages;

public sealed partial class SearchPage : Page
{
    public ObservableCollection<SearchGroup> Groups { get; } = new();

    public SearchPage() => InitializeComponent();

    private async void OnSearchSubmitted(object? sender, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            Groups.Clear();
            if (EmptySearchView != null)
            {
                EmptySearchView.Visibility = Visibility.Visible;
                EmptySearchTitle.Text = "Global Source Search";
                EmptySearchSubtitle.Text = "Type a title or author above to search across all your installed manga and novel sources simultaneously.";
            }
            SearchStatus.Text = string.Empty;
            return;
        }

        SearchStatus.Text = $"Searching sources for \"{query}\"...";
        SearchProgressSkeleton.Visibility = Visibility.Visible;
        if (EmptySearchView != null) EmptySearchView.Visibility = Visibility.Collapsed;

        try
        {
            var results = await AppServices.Extensions.GlobalSearchAsync(query, maxSources: 12);
            Groups.Clear();
            if (results.Count > 0)
            {
                foreach (var g in results)
                {
                    Groups.Add(g);
                }
                SearchStatus.Text = $"{results.Sum(g => g.Results.Count)} results across {results.Count} sources";
                if (EmptySearchView != null) EmptySearchView.Visibility = Visibility.Collapsed;
            }
            else
            {
                SearchStatus.Text = "No results found";
                if (EmptySearchView != null)
                {
                    EmptySearchView.Visibility = Visibility.Visible;
                    EmptySearchTitle.Text = "No Results Found";
                    EmptySearchSubtitle.Text = $"We couldn't find any manga or novels matching \"{query}\". Try another keyword or check individual sources.";
                }
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
        SearchStatus.Text = "";
        Groups.Clear();
        if (EmptySearchView != null)
        {
            EmptySearchView.Visibility = Visibility.Visible;
            EmptySearchTitle.Text = "Global Source Search";
            EmptySearchSubtitle.Text = "Type a title or author above to search across all your installed manga and novel sources simultaneously.";
        }
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
