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

    private Manga _currentManga = Sample.HollowCrown;
    private bool _inLibrary;

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
            if (detail != null && detail.Chapters.Count > 0)
            {
                Chapters.Clear();
                foreach (var ch in detail.Chapters)
                {
                    Chapters.Add(new Chapter(
                        ch.Number,
                        ch.Title,
                        ch.Released,
                        ch.Scanlator ?? _currentManga.Source,
                        "Download",
                        Read: false));
                }
            }
        }
        catch { }
    }

    private async void OnLibraryToggled(object sender, RoutedEventArgs e)
    {
        _inLibrary = !_inLibrary;
        SetInLibrary(_inLibrary);

        try
        {
            if (_inLibrary)
            {
                await AppServices.Database.AddToLibraryAsync(new LibraryEntity
                {
                    Provider = _currentManga.Source,
                    MangaId = _currentManga.Id,
                    Title = _currentManga.Title,
                    CoverUrl = _currentManga.Cover,
                    Type = AppServices.Extensions.IsNovel(_currentManga.Source) ? "novel" : "manga"
                });
                Nav.Toast($"Added \"{_currentManga.Title}\" to library");
            }
            else
            {
                await AppServices.Database.RemoveFromLibraryAsync(_currentManga.Source, _currentManga.Id);
                Nav.Toast($"Removed \"{_currentManga.Title}\" from library");
            }
        }
        catch { }
    }

    private void SetInLibrary(bool value)
    {
        _inLibrary = value;
        InLibrary.Content = value ? "In Library" : "Add to Library";
    }

    private void OnBack(object sender, RoutedEventArgs e) => Nav.Back();

    private void OnResume(object sender, RoutedEventArgs e) =>
        Nav.Go(typeof(ReaderPage), _currentManga);

    private void OnOpenChapter(object sender, RoutedEventArgs e) =>
        Nav.Go(typeof(ReaderPage), _currentManga);

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
