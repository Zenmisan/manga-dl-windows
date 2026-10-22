using System.Collections.ObjectModel;
using MangaDl.Core;
using MangaDl.Helpers;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace MangaDl.Pages;

/// <summary>Browse: Sources, Extensions and Migrate tabs. Pass "sources" or "migrate" to open a tab.</summary>
public sealed partial class ExtensionsPage : Page
{
    public Source LastUsed { get; private set; } = new("MangaDex", "EN · Built-in", "M", "#FF6B6B");
    public ObservableCollection<Source> Sources { get; } = [];
    public ObservableCollection<Extension> Extensions { get; } = [];
    public ObservableCollection<Manga> Library { get; } = [];

    public ExtensionsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            LoadExtensions();
            await LoadLibraryForMigrationAsync();
        };
    }

    private void LoadExtensions()
    {
        try
        {
            var registered = AppServices.Extensions.GetExtensions();
            if (registered.Count > 0)
            {
                Sources.Clear();
                Extensions.Clear();

                foreach (var meta in registered)
                {
                    Sources.Add(new Source(
                        meta.Name,
                        $"{meta.Language} · Built-in",
                        meta.Initial,
                        meta.Color));

                    Extensions.Add(new Extension(
                        meta.Name,
                        meta.Initial,
                        meta.Color,
                        meta.Language,
                        meta.Version,
                        ExtensionState.Installed));
                }

                if (Sources.Count > 0)
                {
                    LastUsed = Sources[0];
                    Bindings.Update();
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("ExtensionsPage.LoadExtensions", ex);
            Nav.Toast("Couldn't load sources");
        }
    }

    private async Task LoadLibraryForMigrationAsync()
    {
        try
        {
            var entities = await AppServices.Database.GetLibraryAsync();
            Library.Clear();
            foreach (var item in entities)
            {
                Library.Add(new Manga(
                    item.MangaId,
                    item.Title,
                    item.CoverUrl ?? "#262626",
                    item.Provider,
                    item.TotalChapters > 0 ? $"{item.TotalChapters} ch" : "Ongoing",
                    true));
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("ExtensionsPage.LoadLibraryForMigrationAsync", ex);
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        switch (e.Parameter as string)
        {
            case "sources": SourcesTab.IsChecked = true; break;
            case "migrate": MigrateTab.IsChecked = true; break;
        }
    }

    private void OnTab(object sender, RoutedEventArgs e)
    {
        if (SourcesPanel is null || ExtensionsPanel is null || MigratePanel is null) return;
        SourcesPanel.Visibility = X.Vis(ReferenceEquals(sender, SourcesTab));
        ExtensionsPanel.Visibility = X.Vis(ReferenceEquals(sender, ExtensionsTab));
        MigratePanel.Visibility = X.Vis(ReferenceEquals(sender, MigrateTab));
    }

    private void OnSearch(object sender, RoutedEventArgs e) => Nav.Go(typeof(SearchPage));
    private void OnOpenSource(object sender, RoutedEventArgs e) => Nav.Go(typeof(BrowseSourcePage), (((FrameworkElement)sender).DataContext as Source)?.Name);

    private async void OnMigrate(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not Manga manga) return;

        var registered = AppServices.Extensions.GetExtensions();
        var sourcesList = registered
            .Where(s => !string.Equals(s.Id, manga.Source, StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Name)
            .ToList();

        var combo = new ComboBox
        {
            ItemsSource = sourcesList,
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var panel = new StackPanel { Width = 360, Spacing = 6 };
        panel.Children.Add(new TextBlock
        {
            Text = $"Select target source to search and migrate \"{manga.Title}\":",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13
        });
        panel.Children.Add(combo);

        var dialog = new ContentDialog
        {
            Title = "Migrate Manga",
            Content = panel,
            PrimaryButtonText = "Search Source",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            Nav.Go(typeof(SearchPage), manga.Title);
            Nav.Toast($"Searching for \"{manga.Title}\" to migrate...");
        }
    }

    private void OnExtensionAction(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is Extension ext)
        {
            Nav.Toast($"{ext.Name} is up to date");
        }
    }

    private void OnPinSource(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is Source src)
        {
            Nav.Toast($"Pinned {src.Name}");
        }
    }

    private void OnUpdateAll(object sender, RoutedEventArgs e) =>
        Nav.Toast("All extensions are up to date");
}
