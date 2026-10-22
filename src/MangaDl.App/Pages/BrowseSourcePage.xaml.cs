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
    public ObservableCollection<Manga> Items { get; } = new();

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

    private void ShowStatusBanner(bool isCloudflare, string title, string message)
    {
        if (SourceStatusBanner == null) return;
        SourceStatusBanner.Visibility = Visibility.Visible;
        SourceStatusTitle.Text = title;
        SourceStatusMessage.Text = message;
        SourceStatusActionButton.Visibility = Visibility.Visible;
    }

    private void HideStatusBanner()
    {
        if (SourceStatusBanner != null) SourceStatusBanner.Visibility = Visibility.Collapsed;
    }

    private async Task LoadPopularAsync()
    {
        try
        {
            var results = await AppServices.Extensions.GetPopularAsync(_sourceId, 1);
            Items.Clear();
            if (results.Count > 0)
            {
                HideStatusBanner();
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
            else
            {
                ShowStatusBanner(
                    isCloudflare: false,
                    title: "No Catalog Items Found",
                    message: $"{_sourceName} returned no items. The source might be under maintenance or experiencing temporary connection issues.");
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("BrowseSourcePage.LoadPopularAsync", ex);
            Items.Clear();
            var isCf = ex.Message.Contains("Cloudflare", StringComparison.OrdinalIgnoreCase) ||
                       ex.Message.Contains("403", StringComparison.OrdinalIgnoreCase) ||
                       ex.Message.Contains("Turnstile", StringComparison.OrdinalIgnoreCase);
            ShowStatusBanner(
                isCloudflare: isCf,
                title: isCf ? "Cloudflare Protection Active" : "Connection Failed",
                message: isCf
                    ? $"{_sourceName} is currently protected by Cloudflare Turnstile verification. Open it in your browser to pass the challenge."
                    : $"Could not connect to {_sourceName}. Check your network connection or verify the source domain in your browser.");
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
            Items.Clear();
            if (results.Count > 0)
            {
                HideStatusBanner();
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
            else
            {
                ShowStatusBanner(
                    isCloudflare: false,
                    title: "No Search Results",
                    message: $"No manga found matching \"{query}\" on {_sourceName}. Try different keywords or check the site in your browser.");
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("BrowseSourcePage.OnSearchSubmitted", ex);
            Items.Clear();
            var isCf = ex.Message.Contains("Cloudflare", StringComparison.OrdinalIgnoreCase) ||
                       ex.Message.Contains("403", StringComparison.OrdinalIgnoreCase);
            ShowStatusBanner(
                isCloudflare: isCf,
                title: isCf ? "Cloudflare Protection Active" : "Search Failed",
                message: isCf
                    ? $"{_sourceName} is protected by Cloudflare. Open in browser to pass verification."
                    : $"Search failed on {_sourceName}. Check your network connection.");
        }
    }

    private void OnRemoveFilter(object sender, RoutedEventArgs e)
    {
        if (FindName((string)((FrameworkElement)sender).Tag) is UIElement chip) chip.Visibility = Visibility.Collapsed;
    }

    private void OnOpenBrowser(object sender, RoutedEventArgs e)
    {
        Nav.Toast($"Opening {_sourceName} in browser");
        try
        {
            var url = _sourceId switch
            {
                "mangadex" => "https://mangadex.org",
                "mangakakalot" => "https://mangakakalot.com",
                "mangakatana" => "https://mangakatana.com",
                _ => $"https://www.google.com/search?q={Uri.EscapeDataString(_sourceName)}"
            };
            _ = Windows.System.Launcher.LaunchUriAsync(new Uri(url));
        }
        catch {}
    }

    private async void OnOpenFilters(object sender, RoutedEventArgs e)
    {
        var fantasyCheck = new CheckBox { Content = "Fantasy", IsChecked = FantasyChip?.Visibility == Visibility.Visible };
        var actionCheck = new CheckBox { Content = "Action", IsChecked = false };
        var romanceCheck = new CheckBox { Content = "Romance", IsChecked = false };
        var comedyCheck = new CheckBox { Content = "Comedy", IsChecked = false };
        var ongoingCheck = new CheckBox { Content = "Ongoing Only", IsChecked = OngoingChip?.Visibility == Visibility.Visible };

        var panel = new StackPanel { Width = 320, Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = "Genres", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 13 });
        panel.Children.Add(fantasyCheck);
        panel.Children.Add(actionCheck);
        panel.Children.Add(romanceCheck);
        panel.Children.Add(comedyCheck);
        panel.Children.Add(new TextBlock { Text = "Status", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 13, Margin = new Thickness(0, 8, 0, 0) });
        panel.Children.Add(ongoingCheck);

        var dialog = new ContentDialog
        {
            Title = $"Filters · {_sourceName}",
            Content = panel,
            PrimaryButtonText = "Apply Filters",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            if (FantasyChip != null)
                FantasyChip.Visibility = fantasyCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            if (OngoingChip != null)
                OngoingChip.Visibility = ongoingCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

            // If a genre is selected, search for that genre keyword
            var activeFilter = fantasyCheck.IsChecked == true ? "Fantasy"
                : (actionCheck.IsChecked == true ? "Action"
                : (romanceCheck.IsChecked == true ? "Romance"
                : (comedyCheck.IsChecked == true ? "Comedy" : null)));

            if (!string.IsNullOrWhiteSpace(activeFilter))
            {
                OnSearchSubmitted(null, activeFilter);
            }
            Nav.Toast($"Filters applied for {_sourceName}");
        }
    }

    private async void OnTabChanged(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Content is string label)
        {
            if (label == "Latest")
            {
                await LoadLatestAsync();
            }
            else
            {
                await LoadPopularAsync();
            }
        }
    }

    private async Task LoadLatestAsync()
    {
        try
        {
            var results = await AppServices.Extensions.GetLatestAsync(_sourceId, 1);
            Items.Clear();
            if (results.Count > 0)
            {
                HideStatusBanner();
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
            else
            {
                ShowStatusBanner(
                    isCloudflare: false,
                    title: "No Latest Chapters Found",
                    message: $"No recent updates found for {_sourceName}.");
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("BrowseSourcePage.LoadLatestAsync", ex);
            Items.Clear();
            var isCf = ex.Message.Contains("Cloudflare", StringComparison.OrdinalIgnoreCase) ||
                       ex.Message.Contains("403", StringComparison.OrdinalIgnoreCase);
            ShowStatusBanner(
                isCloudflare: isCf,
                title: isCf ? "Cloudflare Protection Active" : "Connection Failed",
                message: isCf
                    ? $"{_sourceName} is protected by Cloudflare. Open in browser to pass verification."
                    : $"Could not load latest releases from {_sourceName}.");
        }
    }

    private void OnOpenManga(object sender, RoutedEventArgs e) =>
        Nav.Go(typeof(MangaDetailPage), ((FrameworkElement)sender).DataContext as Manga);

    private void OnBack(object sender, RoutedEventArgs e) => Nav.Back();
}
