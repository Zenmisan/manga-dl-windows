using System.Collections.ObjectModel;
using MangaDl.Core;
using MangaDl.Core.Extensions;
using MangaDl.Core.Local;
using MangaDl.Helpers;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace MangaDl.Pages;

public sealed record NovelChapter(string Id, string Name, bool IsCurrent, bool Read, string Number = "1")
{
    public string Id { get; set; } = Id;
    public string Name { get; set; } = Name;
    public bool IsCurrent { get; set; } = IsCurrent;
    public bool Read { get; set; } = Read;
    public string Number { get; set; } = Number;
}

public sealed partial class NovelReaderPage : Page
{
    public ObservableCollection<NovelChapter> Chapters { get; } = new(
        Enumerable.Range(1, 10).Select(n => new NovelChapter(n.ToString(), $"Ch. {n} · Chapter {n}", n == 1, n < 1, n.ToString())));

    private Manga? _currentNovel;
    private NovelChapter? _activeChapter;

    public NovelReaderPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Nav.SetTitle(_currentNovel != null ? $"{_currentNovel.Title} — Ch. 1" : "[Novel title] — Ch. 1");
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is ReaderNavigationArgs args)
        {
            _currentNovel = args.Manga;
            NovelTitleText.Text = args.Manga.Title;
            Nav.SetTitle($"{args.Manga.Title}");
            await LoadNovelChaptersAsync(args.ChapterId);
        }
        else if (e.Parameter is Manga novel)
        {
            _currentNovel = novel;
            NovelTitleText.Text = novel.Title;
            Nav.SetTitle($"{novel.Title}");
            await LoadNovelChaptersAsync();
        }
    }

    private async Task LoadNovelChaptersAsync(string? targetChapterId = null)
    {
        if (_currentNovel == null) return;
        try
        {
            NovelTitleText.Text = _currentNovel.Title;

            if (_currentNovel.Source == LocalImportService.Provider)
            {
                var downloads = await AppServices.Database.GetDownloadsAsync();
                var local = downloads
                    .Where(d => d.Provider == LocalImportService.Provider && d.MangaId == _currentNovel.Id)
                    .OrderBy(d => d.ChapterNumber)
                    .ToList();

                if (local.Count > 0)
                {
                    Chapters.Clear();
                    for (var i = 0; i < local.Count; i++)
                    {
                        Chapters.Add(new NovelChapter(local[i].ChapterId, local[i].ChapterTitle ?? $"Chapter {i + 1}", i == 0, false, local[i].ChapterNumber.ToString()));
                    }
                    var target = (!string.IsNullOrEmpty(targetChapterId) ? Chapters.FirstOrDefault(c => c.Id == targetChapterId || c.Number == targetChapterId) : null) ?? Chapters[0];
                    await LoadChapterTextAsync(target);
                }
                return;
            }

            var detail = await AppServices.Extensions.GetMangaDetailAsync(_currentNovel.Source, _currentNovel.Id);
            if (detail != null && detail.Chapters.Count > 0)
            {
                Chapters.Clear();
                for (var i = 0; i < detail.Chapters.Count; i++)
                {
                    var ch = detail.Chapters[i];
                    Chapters.Add(new NovelChapter(ch.Id, ch.Title ?? $"Ch. {ch.Number}", i == 0, false, ch.Number));
                }

                if (Chapters.Count > 0)
                {
                    var target = (!string.IsNullOrEmpty(targetChapterId) ? Chapters.FirstOrDefault(c => c.Id == targetChapterId || c.Number == targetChapterId) : null) ?? Chapters[0];
                    await LoadChapterTextAsync(target);
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
        _activeChapter = chapter;
        if (CommentsToggle?.IsChecked == true) _ = LoadChapterCommentsAsync();

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
            ChapterTextResult? result;
            if (_currentNovel.Source == LocalImportService.Provider)
            {
                var download = await AppServices.Database.GetDownloadAsync(LocalImportService.Provider, _currentNovel.Id, chapter.Id);
                var html = download?.CbzPath != null && File.Exists(download.CbzPath)
                    ? await File.ReadAllTextAsync(download.CbzPath)
                    : "";
                result = new ChapterTextResult(html, "html");
            }
            else
            {
                result = await AppServices.Extensions.GetChapterTextAsync(_currentNovel.Source, chapter.Id);
            }
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
            _ = AppServices.Sync.SyncChapterReadAsync(_currentNovel.Source, _currentNovel.Id, chapter.Id, 1, _currentNovel.Title, chapter.Name, isCompleted: true);
            var chNum = double.TryParse(chapter.Number, out var parsedNum) ? parsedNum : 1.0;
            _ = AppServices.SyncTrackerProgressAsync(_currentNovel.Source, _currentNovel.Id, _currentNovel.Title, chNum, isCompleted: true);

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

    private void OnCommentsToggled(object sender, RoutedEventArgs e)
    {
        var open = CommentsToggle?.IsChecked == true;
        if (CommentsDrawer != null) CommentsDrawer.Visibility = X.Vis(open);
        if (open)
        {
            _ = LoadChapterCommentsAsync();
        }
    }

    private void OnCloseCommentsDrawer(object sender, RoutedEventArgs e)
    {
        if (CommentsDrawer != null) CommentsDrawer.Visibility = Visibility.Collapsed;
        if (CommentsToggle != null) CommentsToggle.IsChecked = false;
    }

    private async Task LoadChapterCommentsAsync()
    {
        if (_currentNovel == null) return;
        try
        {
            var userStats = await AppServices.Database.GetAllCompletedProgressAsync();
            var userRank = GamificationService.GetHunterRank(userStats.Count * 10);
            var username = AppServices.Settings.UserName ?? "Hunter";
            if (DrawerPostingAsText != null) DrawerPostingAsText.Text = $"{username} ({userRank.Name})";

            var chId = _activeChapter?.Id ?? _activeChapter?.Number ?? "1";
            var comments = await AppServices.Community.GetCommentsAsync(AppServices.Database, _currentNovel.Source, _currentNovel.Id, chId);

            if (DrawerCommentsTitle != null) DrawerCommentsTitle.Text = $"Comments ({comments.Count})";
            if (DrawerCommentsList == null) return;

            DrawerCommentsList.Children.Clear();
            if (comments.Count == 0)
            {
                DrawerCommentsList.Children.Add(new TextBlock
                {
                    Text = "No comments on this chapter yet. Drop your thoughts below!",
                    FontSize = 12,
                    Foreground = Application.Current.Resources.TryGetValue("FgSubtleBrush", out var sB) ? (Brush)sB : null,
                    Margin = new Thickness(4)
                });
                return;
            }

            foreach (var c in comments)
            {
                DrawerCommentsList.Children.Add(BuildDrawerCommentCard(c));
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("NovelReaderPage.LoadChapterCommentsAsync", ex);
        }
    }

    private UIElement BuildDrawerCommentCard(MangaDl.Core.Community.CommentItem c)
    {
        var card = new Border
        {
            Background = Application.Current.Resources.TryGetValue("SurfaceBrush", out var sB) ? (Brush)sB : null,
            BorderBrush = Application.Current.Resources.TryGetValue("DividerBrush", out var dB) ? (Brush)dB : null,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8)
        };

        var stack = new StackPanel { Spacing = 6 };

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var authorStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var nameText = new TextBlock { Text = c.Username, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 12 };
        var rankBadge = new Border
        {
            Background = Application.Current.Resources.TryGetValue("AccentMutedBrush", out var aM) ? (Brush)aM : null,
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 1, 4, 1),
            Child = new TextBlock
            {
                Text = c.HunterRank,
                FontSize = 10,
                Foreground = Application.Current.Resources.TryGetValue("AccentLightBrush", out var aL) ? (Brush)aL : null
            }
        };
        authorStack.Children.Add(nameText);
        authorStack.Children.Add(rankBadge);
        Grid.SetColumn(authorStack, 0);

        var timeDiff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - c.CreatedAt;
        var hoursAgo = Math.Max(1, (int)(timeDiff / (1000 * 3600)));
        var timeText = new TextBlock
        {
            Text = hoursAgo < 24 ? $"{hoursAgo}h" : $"{hoursAgo / 24}d",
            FontSize = 10,
            Foreground = Application.Current.Resources.TryGetValue("FgSubtleBrush", out var sub) ? (Brush)sub : null,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(timeText, 1);

        headerGrid.Children.Add(authorStack);
        headerGrid.Children.Add(timeText);
        stack.Children.Add(headerGrid);

        var body = new TextBlock { Text = c.Content, FontSize = 12, TextWrapping = TextWrapping.Wrap, LineHeight = 16 };
        stack.Children.Add(body);

        var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(0, 2, 0, 0) };
        var likeBtn = new Button
        {
            Style = Application.Current.Resources.TryGetValue("GhostButton", out var gB) ? (Style)gB : null,
            Padding = new Thickness(6, 2, 6, 2),
            Height = 24,
            FontSize = 10
        };
        var likeContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var likeIcon = new Controls.MdIcon { Kind = c.IsLiked ? "HeartFilled" : "Heart", Size = 11, Foreground = Application.Current.Resources.TryGetValue("AccentLightBrush", out var hB) ? (Brush)hB : null };
        var likeCount = new TextBlock { Text = c.Likes.ToString(), FontSize = 10, VerticalAlignment = VerticalAlignment.Center };
        likeContent.Children.Add(likeIcon);
        likeContent.Children.Add(likeCount);
        likeBtn.Content = likeContent;

        var currentLiked = c.IsLiked;
        var currentLikes = c.Likes;

        likeBtn.Click += async (_, _) =>
        {
            var nowLiked = await AppServices.Community.LikeCommentAsync(AppServices.Database, c.Id);
            currentLiked = nowLiked;
            currentLikes = nowLiked ? currentLikes + 1 : Math.Max(0, currentLikes - 1);
            likeIcon.Kind = currentLiked ? "HeartFilled" : "Heart";
            likeCount.Text = currentLikes.ToString();
        };

        footer.Children.Add(likeBtn);
        stack.Children.Add(footer);

        card.Content = stack;
        return card;
    }

    private async void OnPostDrawerCommentClicked(object sender, RoutedEventArgs e)
    {
        if (DrawerCommentInput == null || _currentNovel == null) return;
        var text = DrawerCommentInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        var userStats = await AppServices.Database.GetAllCompletedProgressAsync();
        var userRank = GamificationService.GetHunterRank(userStats.Count * 10);
        var username = AppServices.Settings.UserName ?? "Hunter";
        var chId = _activeChapter?.Id ?? _activeChapter?.Number ?? "1";

        await AppServices.Community.PostCommentAsync(
            AppServices.Database,
            _currentNovel.Source,
            _currentNovel.Id,
            chapterId: chId,
            username: username,
            userAvatar: null,
            hunterRank: userRank.Name,
            content: text);

        DrawerCommentInput.Text = string.Empty;
        await LoadChapterCommentsAsync();
        Nav.Toast("Comment posted!");
    }
}
