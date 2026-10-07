using System.Collections.ObjectModel;
using MangaDl.Core;
using MangaDl.Core.Database.Entities;
using MangaDl.Helpers;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace MangaDl.Pages;

public sealed partial class MangaDetailPage : Page
{
    public ObservableCollection<Chapter> Chapters { get; } = new(Sample.Chapters);

    private readonly List<Chapter> _allChapters = [];
    private Manga _currentManga = Sample.HollowCrown;
    private bool _inLibrary;
    private bool _newestFirst = true;

    public MangaDetailPage() => InitializeComponent();

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _currentManga = e.Parameter as Manga ?? Sample.HollowCrown;

        // Ambilight: hero tinted with the cover colour, cover a darker shade of it.
        Hero.Background = X.Hex(_currentManga.Cover);
        var c = X.ToColor(_currentManga.Cover);
        Cover.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            Microsoft.UI.ColorHelper.FromArgb(255, (byte)(c.R * 0.6), (byte)(c.G * 0.6), (byte)(c.B * 0.6)));

        TitleText.Text = _currentManga.Title.ToUpperInvariant();
        MetaText.Text = $"Ongoing · {_currentManga.Source}";

        var inDb = await AppServices.Database.IsInLibraryAsync(_currentManga.Source, _currentManga.Id);
        SetInLibrary(inDb || _currentManga.InLibrary);

        await LoadChaptersAsync();
    }

    private async Task LoadChaptersAsync()
    {
        try
        {
            var detail = await AppServices.Extensions.GetMangaDetailAsync(_currentManga.Source, _currentManga.Id);
            if (detail != null)
            {
                if (!string.IsNullOrWhiteSpace(detail.Description))
                {
                    SynopsisText.Text = detail.Description;
                }

                if (detail.Chapters.Count > 0)
                {
                    _allChapters.Clear();
                    foreach (var ch in detail.Chapters)
                    {
                        _allChapters.Add(new Chapter(
                            ch.Number,
                            ch.Title,
                            ch.Released,
                            ch.Scanlator ?? _currentManga.Source,
                            "Download",
                            Read: false));
                    }
                    ApplyChapterFilters();
                    ResumeText.Text = $"Resume {_allChapters.First().Number}";
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("MangaDetailPage.LoadChaptersAsync", ex);
            Nav.Toast("Couldn't load chapters");
        }
    }

    private void ApplyChapterFilters()
    {
        var filtered = _allChapters.AsEnumerable();

        if (FilterUnread?.IsChecked == true)
        {
            filtered = filtered.Where(ch => !ch.Read);
        }

        if (FilterDownloaded?.IsChecked == true)
        {
            filtered = filtered.Where(ch => ch.Status == "Downloaded");
        }

        filtered = _newestFirst ? filtered.OrderByDescending(ch => ch.Number) : filtered.OrderBy(ch => ch.Number);

        Chapters.Clear();
        foreach (var ch in filtered) Chapters.Add(ch);
        ChapterCountText.Text = $"{Chapters.Count} chapters";
    }

    private void OnFilterChanged(object sender, RoutedEventArgs e) => ApplyChapterFilters();

    private void OnSortChapters(object sender, RoutedEventArgs e)
    {
        _newestFirst = !_newestFirst;
        SortChaptersButton.Content = _newestFirst ? "Newest first" : "Oldest first";
        ApplyChapterFilters();
    }

    private async void OnLibraryToggled(object sender, RoutedEventArgs e)
    {
        _inLibrary = !_inLibrary;
        SetInLibrary(_inLibrary);

        try
        {
            var libEntity = new LibraryEntity
            {
                Provider = _currentManga.Source,
                MangaId = _currentManga.Id,
                Title = _currentManga.Title,
                CoverUrl = _currentManga.Cover,
                Type = AppServices.Extensions.IsNovel(_currentManga.Source) ? "novel" : "manga"
            };

            if (_inLibrary)
            {
                await AppServices.Database.AddToLibraryAsync(libEntity);
                _ = AppServices.Sync.SyncMangaSubscriptionAsync(libEntity, subscribed: true);
                Nav.Toast($"Added \"{_currentManga.Title}\" to library");
            }
            else
            {
                await AppServices.Database.RemoveFromLibraryAsync(_currentManga.Source, _currentManga.Id);
                _ = AppServices.Sync.SyncMangaSubscriptionAsync(libEntity, subscribed: false);
                Nav.Toast($"Removed \"{_currentManga.Title}\" from library");
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("MangaDetailPage.OnLibraryToggled", ex);
            SetInLibrary(!_inLibrary);
            Nav.Toast("Couldn't update library");
        }
    }

    private void SetInLibrary(bool value)
    {
        _inLibrary = value;
        InLibrary.Content = value ? "In Library" : "Add to Library";
    }

    private void OnBack(object sender, RoutedEventArgs e) => Nav.Back();

    private void OnTrack(object sender, RoutedEventArgs e) =>
        Nav.Go(typeof(Settings.TrackersSettingsPage));

    private void OnResume(object sender, RoutedEventArgs e)
    {
        if (AppServices.Extensions.IsNovel(_currentManga.Source))
        {
            Nav.Go(typeof(NovelReaderPage), _currentManga);
        }
        else
        {
            Nav.Go(typeof(ReaderPage), _currentManga);
        }
    }

    private void OnOpenChapter(object sender, RoutedEventArgs e)
    {
        if (AppServices.Extensions.IsNovel(_currentManga.Source))
        {
            Nav.Go(typeof(NovelReaderPage), _currentManga);
        }
        else
        {
            Nav.Go(typeof(ReaderPage), _currentManga);
        }
    }

    private async void OnDownload(object sender, RoutedEventArgs e)
    {
        Nav.Toast($"Queuing {Chapters.Count} chapters for download");
        foreach (var ch in Chapters)
        {
            if (double.TryParse(ch.Number, out var num))
            {
                await AppServices.Downloads.EnqueueChapterAsync(
                    _currentManga.Source,
                    _currentManga.Id,
                    _currentManga.Title,
                    ch.Number,
                    ch.Title,
                    num);
            }
        }
    }

    private void OnMigrate(object sender, RoutedEventArgs e) =>
        Nav.Go(typeof(ExtensionsPage), "migrate");
}
