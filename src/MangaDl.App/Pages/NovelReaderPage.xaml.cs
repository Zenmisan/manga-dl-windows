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

public sealed record NovelChapter(string Id, string Name, bool IsCurrent, bool Read);

public sealed partial class NovelReaderPage : Page
{
    public ObservableCollection<NovelChapter> Chapters { get; } = new(
        Enumerable.Range(1, 10).Select(n => new NovelChapter(n.ToString(), $"Ch. {n} · Chapter {n}", n == 1, n < 1)));

    private Manga? _currentNovel;

    public NovelReaderPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Nav.SetTitle(_currentNovel != null ? $"{_currentNovel.Title} — Ch. 1" : "[Novel title] — Ch. 1");
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is Manga novel)
        {
            _currentNovel = novel;
            NovelTitleText.Text = novel.Title;
            Nav.SetTitle($"{novel.Title}");
            await LoadNovelChaptersAsync();
        }
    }

    private async Task LoadNovelChaptersAsync()
    {
        if (_currentNovel == null) return;
        try
        {
            NovelTitleText.Text = _currentNovel.Title;
            var detail = await AppServices.Extensions.GetMangaDetailAsync(_currentNovel.Source, _currentNovel.Id);
            if (detail != null && detail.Chapters.Count > 0)
            {
                Chapters.Clear();
                for (var i = 0; i < detail.Chapters.Count; i++)
                {
                    var ch = detail.Chapters[i];
                    Chapters.Add(new NovelChapter(ch.Id, ch.Title ?? $"Ch. {ch.Number}", i == 0, false));
                }

                if (Chapters.Count > 0)
                {
                    await LoadChapterTextAsync(Chapters[0]);
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("NovelReaderPage.LoadNovelChaptersAsync", ex);
            Nav.Toast("Couldn't load chapter list");
        }
    }

    private async void OnChapterSelected(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is NovelChapter ch && _currentNovel != null)
        {
            await LoadChapterTextAsync(ch);
        }
    }

    private async Task LoadChapterTextAsync(NovelChapter chapter)
    {
        if (_currentNovel == null) return;

        NovelTitleText.Text = _currentNovel.Title;
        NovelChapterText.Text = $" · {chapter.Name}";
        ChapterHeading.Text = chapter.Name.ToUpperInvariant();
        Nav.SetTitle($"{_currentNovel.Title} — {chapter.Name}");

        for (var i = 0; i < Chapters.Count; i++)
        {
            var isCurr = Chapters[i].Id == chapter.Id;
            if (Chapters[i].IsCurrent != isCurr)
            {
                Chapters[i] = Chapters[i] with { IsCurrent = isCurr };
            }
        }

        P1.Text = "Loading chapter...";
        P2.Text = string.Empty;
        P3.Text = string.Empty;

        try
        {
            var result = await AppServices.Extensions.GetChapterTextAsync(_currentNovel.Source, chapter.Id);
            var text = result?.Content ?? string.Empty;
            if (result?.Format == "html" && !string.IsNullOrEmpty(text))
            {
                text = System.Text.RegularExpressions.Regex.Replace(text, "<br\\s*/?>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                text = System.Text.RegularExpressions.Regex.Replace(text, "</p>", "\n\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", " ");
                text = System.Net.WebUtility.HtmlDecode(text).Trim();
            }
            if (!string.IsNullOrWhiteSpace(text))
            {
                var paragraphs = text.Split(new[] { "\r\n\r\n", "\n\n", "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (paragraphs.Length >= 3)
                {
                    P1.Text = paragraphs[0];
                    P2.Text = paragraphs[1];
                    P3.Text = string.Join("\n\n", paragraphs.Skip(2));
                }
                else if (paragraphs.Length == 2)
                {
                    P1.Text = paragraphs[0];
                    P2.Text = paragraphs[1];
                    P3.Text = string.Empty;
                }
                else if (paragraphs.Length == 1)
                {
                    P1.Text = paragraphs[0];
                    P2.Text = string.Empty;
                    P3.Text = string.Empty;
                }
            }
            else
            {
                P1.Text = "[No content available for this chapter]";
            }

            _ = AppServices.Database.SaveProgressAsync(_currentNovel.Source, _currentNovel.Id, chapter.Id, 1.0, 1, true)
                .ContinueWith(t => AppLog.Warn("NovelReaderPage.SaveProgressAsync", t.Exception!), TaskContinuationOptions.OnlyOnFaulted);
            _ = AppServices.Database.RecordHistoryAsync(_currentNovel.Source, _currentNovel.Id, _currentNovel.Title, chapter.Id, chapter.Name, _currentNovel.Cover)
                .ContinueWith(t => AppLog.Warn("NovelReaderPage.RecordHistoryAsync", t.Exception!), TaskContinuationOptions.OnlyOnFaulted);

            Article?.ChangeView(0, 0, 1.0f);
        }
        catch (Exception ex)
        {
            AppLog.Warn("NovelReaderPage.LoadChapterTextAsync", ex);
            P1.Text = "[Error loading chapter text. Check internet connection.]";
            Nav.Toast("Couldn't load chapter text");
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
