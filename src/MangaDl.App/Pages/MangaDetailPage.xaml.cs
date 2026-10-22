using System.Collections.ObjectModel;
using MangaDl.Core;
using MangaDl.Core.Database.Entities;
using MangaDl.Core.Local;
using MangaDl.Dialogs;
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
    private bool _isNovel;

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

        var libraryEntry = await AppServices.Database.GetLibraryItemAsync(_currentManga.Source, _currentManga.Id);
        SetInLibrary(libraryEntry != null || _currentManga.InLibrary);

        // "local" is shared by both locally-imported manga and novels (LocalImportService),
        // so unlike every other provider it can't be classified by source id alone —
        // IsNovel(source) only knows about real extension providers. Fall back to the
        // actual library row's Type when there is one.
        _isNovel = libraryEntry?.Type == "novel" || AppServices.Extensions.IsNovel(_currentManga.Source);

        await LoadChaptersAsync();
        _ = LoadCommentsAsync();
    }

    private async Task LoadChaptersAsync()
    {
        try
        {
            var progressList = await AppServices.Database.GetMangaProgressAsync(_currentManga.Source, _currentManga.Id);
            var readChapterIds = progressList.Where(p => p.Completed == 1).Select(p => p.ChapterId).ToHashSet();
            var readChapterNums = progressList.Where(p => p.Completed == 1).Select(p => p.ChapterNumber).ToHashSet();

            var bookmarkList = await AppServices.Database.GetBookmarksAsync(_currentManga.Source, _currentManga.Id);
            var bookmarkedIds = bookmarkList.Select(b => b.ChapterId).ToHashSet();
            var bookmarkedNums = bookmarkList.Select(b => b.ChapterNumber).ToHashSet();

            var allDownloads = await AppServices.Database.GetDownloadsAsync();
            var downloadedIds = allDownloads
                .Where(d => d.Provider == _currentManga.Source && d.MangaId == _currentManga.Id && d.Status == "completed")
                .Select(d => d.ChapterId)
                .ToHashSet();
            var downloadedNums = allDownloads
                .Where(d => d.Provider == _currentManga.Source && d.MangaId == _currentManga.Id && d.Status == "completed")
                .Select(d => d.ChapterNumber)
                .ToHashSet();

            if (_currentManga.Source == LocalImportService.Provider)
            {
                var local = allDownloads
                    .Where(d => d.Provider == LocalImportService.Provider && d.MangaId == _currentManga.Id)
                    .OrderBy(d => d.ChapterNumber)
                    .ToList();

                _allChapters.Clear();
                foreach (var d in local)
                {
                    var chId = d.ChapterId ?? d.ChapterNumber.ToString();
                    var isRead = readChapterIds.Contains(chId) || readChapterNums.Contains(d.ChapterNumber);
                    var isBookmarked = bookmarkedIds.Contains(chId) || bookmarkedNums.Contains(d.ChapterNumber);

                    _allChapters.Add(new Chapter(
                        d.ChapterNumber.ToString(),
                        d.ChapterTitle ?? $"Chapter {d.ChapterNumber}",
                        "",
                        "Local",
                        "Downloaded",
                        Read: isRead,
                        Bookmarked: isBookmarked,
                        Id: chId));
                }
                PopulateGroupFilters();
                ApplyChapterFilters();
                UpdateResumeButton();
                return;
            }

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
                        double.TryParse(ch.Number, out var cNum);
                        var isRead = readChapterIds.Contains(ch.Id) || (cNum > 0 && readChapterNums.Contains(cNum));
                        var isBookmarked = bookmarkedIds.Contains(ch.Id) || (cNum > 0 && bookmarkedNums.Contains(cNum));
                        var isDownloaded = downloadedIds.Contains(ch.Id) || (cNum > 0 && downloadedNums.Contains(cNum));

                        _allChapters.Add(new Chapter(
                            ch.Number,
                            ch.Title,
                            ch.Released,
                            ch.Scanlator ?? _currentManga.Source,
                            isDownloaded ? "Downloaded" : "Download",
                            Read: isRead,
                            Bookmarked: isBookmarked,
                            Id: ch.Id));
                    }
                    PopulateGroupFilters();
                    ApplyChapterFilters();
                    UpdateResumeButton();
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("MangaDetailPage.LoadChaptersAsync", ex);
            Nav.Toast("Couldn't load chapters");
        }
    }

    private void PopulateGroupFilters()
    {
        var groups = _allChapters
            .Select(c => c.Group)
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Distinct()
            .OrderBy(g => g)
            .ToList();

        GroupFilterComboBox.Items.Clear();
        GroupFilterComboBox.Items.Add("All Groups");
        foreach (var g in groups) GroupFilterComboBox.Items.Add(g);
        GroupFilterComboBox.SelectedIndex = 0;
    }

    private void UpdateResumeButton()
    {
        var unread = _allChapters
            .Where(c => !c.Read)
            .OrderBy(c => double.TryParse(c.Number, out var n) ? n : 999999)
            .FirstOrDefault();

        if (unread != null)
        {
            ResumeText.Text = $"Resume Ch. {unread.Number}";
        }
        else if (_allChapters.Count > 0)
        {
            ResumeText.Text = $"Read Ch. {_allChapters.First().Number}";
        }
    }

    private void ApplyChapterFilters()
    {
        var filtered = _allChapters.AsEnumerable();

        if (GroupFilterComboBox?.SelectedItem is string selectedGroup && selectedGroup != "All Groups")
        {
            filtered = filtered.Where(ch => ch.Group == selectedGroup);
        }

        if (FilterUnread?.IsChecked == true)
        {
            filtered = filtered.Where(ch => !ch.Read);
        }

        if (FilterDownloaded?.IsChecked == true)
        {
            filtered = filtered.Where(ch => ch.Status == "Downloaded");
        }

        if (FilterBookmarked?.IsChecked == true)
        {
            filtered = filtered.Where(ch => ch.Bookmarked);
        }

        filtered = _newestFirst 
            ? filtered.OrderByDescending(ch => double.TryParse(ch.Number, out var n) ? n : 0) 
            : filtered.OrderBy(ch => double.TryParse(ch.Number, out var n) ? n : 0);

        Chapters.Clear();
        foreach (var ch in filtered) Chapters.Add(ch);
        ChapterCountText.Text = $"{Chapters.Count} chapters";
    }

    private void OnGroupFilterChanged(object sender, SelectionChangedEventArgs e) => ApplyChapterFilters();

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
                Type = _isNovel ? "novel" : "manga"
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

    private async void OnCategoriesClicked(object sender, RoutedEventArgs e)
    {
        if (_currentManga == null) return;
        try
        {
            var saved = await CategoryDialogHelper.ShowAsync(
                XamlRoot,
                _currentManga.Source,
                _currentManga.Id,
                _currentManga.Title);

            if (saved)
            {
                Nav.Toast($"Categories updated for \"{_currentManga.Title}\"");
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("MangaDetailPage.OnCategoriesClicked", ex);
            Nav.Toast("Couldn't update categories");
        }
    }

    private async void OnTrack(object sender, RoutedEventArgs e)
    {
        if (_currentManga == null) return;
        try
        {
            await TrackerBindingDialogHelper.ShowAsync(XamlRoot, _currentManga);
        }
        catch (Exception ex)
        {
            AppLog.Warn("MangaDetailPage.OnTrack", ex);
            Nav.Toast("Couldn't open tracker settings");
        }
    }

    private async void OnMarkReadClicked(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not Chapter ch) return;
        double.TryParse(ch.Number, out var chNum);
        var chId = ch.Id ?? ch.Number;

        await AppServices.Database.MarkChaptersReadAsync(
            _currentManga.Source,
            _currentManga.Id,
            [(chId, chNum, ch.Title)],
            _currentManga.Cover,
            _currentManga.Title);

        _ = AppServices.SyncTrackerProgressAsync(
            _currentManga.Source,
            _currentManga.Id,
            _currentManga.Title,
            chNum > 0 ? chNum : 1.0,
            isCompleted: false);

        ch.Read = true;
        ApplyChapterFilters();
        UpdateResumeButton();
        Nav.Toast($"Marked Chapter {ch.Number} as read");
    }

    private async void OnMarkUnreadClicked(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not Chapter ch) return;
        var chId = ch.Id ?? ch.Number;

        await AppServices.Database.DeleteProgressAsync(_currentManga.Source, _currentManga.Id, chId);

        ch.Read = false;
        ApplyChapterFilters();
        UpdateResumeButton();
        Nav.Toast($"Marked Chapter {ch.Number} as unread");
    }

    private async void OnMarkPreviousReadClicked(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not Chapter ch) return;
        double.TryParse(ch.Number, out var targetNum);
        if (targetNum <= 0) return;

        var toMark = _allChapters
            .Where(c => double.TryParse(c.Number, out var n) && n <= targetNum)
            .Select(c => (c.Id ?? c.Number, double.TryParse(c.Number, out var n) ? n : 0, (string?)c.Title))
            .ToList();

        if (toMark.Count == 0) return;

        await AppServices.Database.MarkChaptersReadAsync(
            _currentManga.Source,
            _currentManga.Id,
            toMark,
            _currentManga.Cover,
            _currentManga.Title);

        _ = AppServices.SyncTrackerProgressAsync(
            _currentManga.Source,
            _currentManga.Id,
            _currentManga.Title,
            targetNum,
            isCompleted: false);

        foreach (var c in _allChapters)
        {
            if (double.TryParse(c.Number, out var n) && n <= targetNum)
            {
                c.Read = true;
            }
        }

        ApplyChapterFilters();
        UpdateResumeButton();
        Nav.Toast($"Marked {toMark.Count} previous chapters as read");
    }

    private async void OnBookmarkChapterClicked(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not Chapter ch) return;
        double.TryParse(ch.Number, out var chNum);
        var chId = ch.Id ?? ch.Number;

        if (ch.Bookmarked)
        {
            await AppServices.Database.RemoveBookmarkAsync(_currentManga.Source, _currentManga.Id, chId);
            ch.Bookmarked = false;
            Nav.Toast($"Removed bookmark from Chapter {ch.Number}");
        }
        else
        {
            await AppServices.Database.AddBookmarkAsync(new BookmarkEntity
            {
                Provider = _currentManga.Source,
                MangaId = _currentManga.Id,
                ChapterId = chId,
                ChapterNumber = chNum,
                ChapterTitle = ch.Title
            });
            ch.Bookmarked = true;
            Nav.Toast($"Bookmarked Chapter {ch.Number}");
        }

        ApplyChapterFilters();
    }

    private async void OnDownloadChapterClicked(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not Chapter ch) return;
        double.TryParse(ch.Number, out var num);
        var chId = ch.Id ?? ch.Number;

        await AppServices.Downloads.EnqueueChapterAsync(
            _currentManga.Source,
            _currentManga.Id,
            _currentManga.Title,
            chId,
            ch.Title,
            num);

        Nav.Toast($"Queued Chapter {ch.Number} for download");
    }

    private void OnResume(object sender, RoutedEventArgs e)
    {
        var first = _allChapters.FirstOrDefault();
        if (first != null)
        {
            double.TryParse(first.Number, out var num);
            var args = new ReaderNavigationArgs(_currentManga, first.Number, first.Title, num > 0 ? num : 1.0);
            Nav.Go(_isNovel ? typeof(NovelReaderPage) : typeof(ReaderPage), args);
        }
        else
        {
            Nav.Go(_isNovel ? typeof(NovelReaderPage) : typeof(ReaderPage), _currentManga);
        }
    }

    private void OnOpenChapter(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is Chapter ch)
        {
            double.TryParse(ch.Number, out var num);
            var args = new ReaderNavigationArgs(_currentManga, ch.Number, ch.Title, num > 0 ? num : 1.0);
            Nav.Go(_isNovel ? typeof(NovelReaderPage) : typeof(ReaderPage), args);
        }
        else
        {
            Nav.Go(_isNovel ? typeof(NovelReaderPage) : typeof(ReaderPage), _currentManga);
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

    private async Task LoadCommentsAsync()
    {
        try
        {
            var userStats = await AppServices.Database.GetAllCompletedProgressAsync();
            var userRank = GamificationService.GetHunterRank(userStats.Count * 10);
            var username = AppServices.Settings.UserName ?? "Hunter";
            PostingAsText.Text = $"Posting as: {username} ({userRank.Name})";

            var comments = await AppServices.Community.GetCommentsAsync(AppServices.Database, _currentManga.Source, _currentManga.Id, chapterId: null);
            CommentsCountText.Text = $"Community Discussion ({comments.Count})";

            CommentsListPanel.Children.Clear();
            if (comments.Count == 0)
            {
                CommentsListPanel.Children.Add(new TextBlock
                {
                    Text = "No comments yet. Be the first to start the discussion!",
                    FontSize = 13,
                    Foreground = Application.Current.Resources.TryGetValue("FgSubtleBrush", out var sBrush) ? (Brush)sBrush : null,
                    Margin = new Thickness(4)
                });
                return;
            }

            foreach (var c in comments)
            {
                CommentsListPanel.Children.Add(BuildCommentCard(c));
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("MangaDetailPage.LoadCommentsAsync", ex);
        }
    }

    private UIElement BuildCommentCard(MangaDl.Core.Community.CommentItem c)
    {
        var card = new Border
        {
            Background = Application.Current.Resources.TryGetValue("SurfaceBrush", out var sB) ? (Brush)sB : null,
            BorderBrush = Application.Current.Resources.TryGetValue("DividerBrush", out var dB) ? (Brush)dB : null,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 12, 14, 12)
        };

        var stack = new StackPanel { Spacing = 8 };

        // Header: Initial circle, Username, Rank badge, Timestamp
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var authorStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        var initial = string.IsNullOrWhiteSpace(c.Username) ? "H" : c.Username[0].ToString().ToUpperInvariant();
        var avatarBorder = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(14),
            Background = Application.Current.Resources.TryGetValue("DarkChipBrush", out var cB) ? (Brush)cB : null,
            Child = new TextBlock
            {
                Text = initial,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        var nameText = new TextBlock
        {
            Text = c.Username,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center
        };

        var rankBorder = new Border
        {
            Background = Application.Current.Resources.TryGetValue("AccentMutedBrush", out var aM) ? (Brush)aM : null,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = c.HunterRank,
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = Application.Current.Resources.TryGetValue("AccentLightBrush", out var aL) ? (Brush)aL : null
            }
        };

        authorStack.Children.Add(avatarBorder);
        authorStack.Children.Add(nameText);
        authorStack.Children.Add(rankBorder);
        Grid.SetColumn(authorStack, 0);

        var timeDiff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - c.CreatedAt;
        var hoursAgo = Math.Max(1, (int)(timeDiff / (1000 * 3600)));
        var timeText = new TextBlock
        {
            Text = hoursAgo < 24 ? $"{hoursAgo}h ago" : $"{hoursAgo / 24}d ago",
            FontSize = 11,
            Foreground = Application.Current.Resources.TryGetValue("FgSubtleBrush", out var sub) ? (Brush)sub : null,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(timeText, 1);

        headerGrid.Children.Add(authorStack);
        headerGrid.Children.Add(timeText);
        stack.Children.Add(headerGrid);

        // Content
        var contentText = new TextBlock
        {
            Text = c.Content,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 18
        };
        stack.Children.Add(contentText);

        // Footer: Like button
        var footerStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 2, 0, 0) };
        var likeBtn = new Button
        {
            Style = Application.Current.Resources.TryGetValue("GhostButton", out var gB) ? (Style)gB : null,
            Padding = new Thickness(8, 2, 8, 2),
            Height = 26,
            FontSize = 11
        };

        var likeContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var likeIcon = new Controls.MdIcon { Kind = c.IsLiked ? "HeartFilled" : "Heart", Size = 13, Foreground = Application.Current.Resources.TryGetValue("AccentLightBrush", out var hB) ? (Brush)hB : null };
        var likeCountText = new TextBlock { Text = c.Likes.ToString(), FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
        likeContent.Children.Add(likeIcon);
        likeContent.Children.Add(likeCountText);
        likeBtn.Content = likeContent;

        var currentLiked = c.IsLiked;
        var currentLikes = c.Likes;

        likeBtn.Click += async (_, _) =>
        {
            var nowLiked = await AppServices.Community.LikeCommentAsync(AppServices.Database, c.Id);
            currentLiked = nowLiked;
            currentLikes = nowLiked ? currentLikes + 1 : Math.Max(0, currentLikes - 1);
            likeIcon.Kind = currentLiked ? "HeartFilled" : "Heart";
            likeCountText.Text = currentLikes.ToString();
        };

        footerStack.Children.Add(likeBtn);
        stack.Children.Add(footerStack);

        card.Content = stack;
        return card;
    }

    private void OnRefreshCommentsClicked(object sender, RoutedEventArgs e) => _ = LoadCommentsAsync();

    private async void OnPostCommentClicked(object sender, RoutedEventArgs e)
    {
        var text = CommentInputBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        var userStats = await AppServices.Database.GetAllCompletedProgressAsync();
        var userRank = GamificationService.GetHunterRank(userStats.Count * 10);
        var username = AppServices.Settings.UserName ?? "Hunter";

        await AppServices.Community.PostCommentAsync(
            AppServices.Database,
            _currentManga.Source,
            _currentManga.Id,
            chapterId: null,
            username: username,
            userAvatar: null,
            hunterRank: userRank.Name,
            content: text);

        CommentInputBox.Text = string.Empty;
        await LoadCommentsAsync();
        Nav.Toast("Comment posted!");
    }
}
