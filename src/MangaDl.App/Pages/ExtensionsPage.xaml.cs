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
    public Source LastUsed { get; } = Sample.LastUsedSource;
    public ObservableCollection<Source> Sources { get; } = new(Sample.Sources);
    public ObservableCollection<Extension> Extensions { get; } = new(Sample.Extensions);
    public IReadOnlyList<Manga> Library { get; } = Sample.Library.Take(8).ToList();

    public ExtensionsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadExtensions();
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
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("ExtensionsPage.LoadExtensions", ex);
            Nav.Toast("Couldn't load sources");
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
        SourcesPanel.Visibility = X.Vis(sender == SourcesTab);
        ExtensionsPanel.Visibility = X.Vis(sender == ExtensionsTab);
        MigratePanel.Visibility = X.Vis(sender == MigrateTab);
    }

    private void OnSearch(object sender, RoutedEventArgs e) => Nav.Go(typeof(SearchPage));
    private void OnOpenSource(object sender, RoutedEventArgs e) => Nav.Go(typeof(BrowseSourcePage), (((FrameworkElement)sender).DataContext as Source)?.Name);

    private void OnMigrate(object sender, RoutedEventArgs e) => Nav.Toast("Pick a target source to migrate to");
}
