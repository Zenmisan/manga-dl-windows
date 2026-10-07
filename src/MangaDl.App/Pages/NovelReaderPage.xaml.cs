using System.Collections.ObjectModel;
using MangaDl.Core;
using MangaDl.Helpers;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace MangaDl.Pages;

public sealed record NovelChapter(string Name, bool IsCurrent, bool Read);

public sealed partial class NovelReaderPage : Page
{
    public ObservableCollection<NovelChapter> Chapters { get; } = new(
        Enumerable.Range(8, 11).Select(n => new NovelChapter($"Ch. {n} · [title]", n == 12, n < 12)));

    private Manga? _currentNovel;

    public NovelReaderPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Nav.SetTitle(_currentNovel != null ? $"{_currentNovel.Title} — Ch. 12" : "[Novel title] — Ch. 12");
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is Manga novel)
        {
            _currentNovel = novel;
            Nav.SetTitle($"{novel.Title} — Ch. 12");
            await LoadNovelChaptersAsync();
        }
    }

    private async Task LoadNovelChaptersAsync()
    {
        if (_currentNovel == null) return;
        try
        {
            var detail = await AppServices.Extensions.GetMangaDetailAsync(_currentNovel.Source, _currentNovel.Id);
            if (detail != null && detail.Chapters.Count > 0)
            {
                Chapters.Clear();
                for (var i = 0; i < detail.Chapters.Count; i++)
                {
                    var ch = detail.Chapters[i];
                    Chapters.Add(new NovelChapter(ch.Title ?? $"Ch. {ch.Number}", i == 0, false));
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("NovelReaderPage.LoadNovelChaptersAsync", ex);
            Nav.Toast("Couldn't load chapter list");
        }
    }

    private IEnumerable<TextBlock> Paragraphs => new[] { P1, P2, P3 }.Where(p => p is not null);

    private void OnSerif(object sender, RoutedEventArgs e)
    {
        foreach (var p in Paragraphs) p.FontFamily = (FontFamily)Application.Current.Resources["SerifFont"];
    }

    private void OnSans(object sender, RoutedEventArgs e)
    {
        foreach (var p in Paragraphs) p.FontFamily = (FontFamily)Application.Current.Resources["InterFont"];
    }

    private void OnSize(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (SizeText is null) return;
        SizeText.Text = $"{(int)e.NewValue}px";
        foreach (var p in Paragraphs)
        {
            p.FontSize = e.NewValue;
            p.LineHeight = e.NewValue * 1.75;
        }
    }

    private void OnTheme(object sender, RoutedEventArgs e)
    {
        var (bg, fg) = ((FrameworkElement)sender).Tag switch
        {
            "Black" => ("#000000", "#DBFFFFFF"),
            "Sepia" => ("#E8DCC4", "#FF3B2F22"),
            "Light" => ("#F5F5F2", "#FF1F1F1F"),
            _ => ("#0D0D0D", "#DBFFFFFF"),
        };
        Article.Background = X.Hex(bg);
        foreach (var p in Paragraphs) p.Foreground = X.Hex(fg);
    }

    private void OnClose(object sender, RoutedEventArgs e) => Nav.Back();
}
