using MangaDl.Pages;
using MangaDl.Pages.Settings;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace MangaDl.Services;

/// <summary>Where a page sits in the window chrome.</summary>
public enum Chrome
{
    /// <summary>Sidebar visible (normal app pages).</summary>
    App,
    /// <summary>No sidebar (first run, readers).</summary>
    FullBleed,
}

/// <summary>
/// App-wide navigation. Pages call Nav.Go(typeof(SomePage)); MainWindow listens
/// to update the sidebar, title bar and chrome.
/// </summary>
public static class Nav
{
    public static Frame? Frame { get; set; }

    /// <summary>Sidebar item each page highlights (null = none).</summary>
    public static readonly Dictionary<Type, string?> SidebarKey = new()
    {
        [typeof(LibraryPage)] = "Library",
        [typeof(MangaDetailPage)] = "Library",
        [typeof(ImportPage)] = "Library",
        [typeof(UpdatesPage)] = "Updates",
        [typeof(HistoryPage)] = "History",
        [typeof(ExtensionsPage)] = "Browse",
        [typeof(SearchPage)] = "Browse",
        [typeof(BrowseSourcePage)] = "Browse",
        [typeof(DownloadsPage)] = "Downloads",
        [typeof(StatsPage)] = "Stats",
        [typeof(NotificationsPage)] = "Notifications",
        [typeof(SettingsPage)] = "Settings",
        [typeof(HelpPage)] = "Help",
        [typeof(ProfilePage)] = null,
        [typeof(AllPagesPage)] = "AllPages",
    };

    /// <summary>Pages that hide the sidebar.</summary>
    public static readonly HashSet<Type> FullBleed =
    [
        typeof(OnboardingPage),
        typeof(LoginPage),
        typeof(RegisterPage),
        typeof(ForgotPasswordPage),
        typeof(ReaderPage),
        typeof(NovelReaderPage),
    ];

    public static Chrome ChromeFor(Type page) => FullBleed.Contains(page) ? Chrome.FullBleed : Chrome.App;

    public static void Go(Type page, object? parameter = null)
    {
        // Fade is handled by MainWindow (design.md: fade only, no slides).
        Frame?.Navigate(page, parameter, new SuppressNavigationTransitionInfo());
    }

    public static void Back()
    {
        if (Frame?.CanGoBack == true) Frame.GoBack(new SuppressNavigationTransitionInfo());
        else Go(typeof(LibraryPage));
    }

    /// <summary>Clears history and lands on the library (after sign in / onboarding).</summary>
    public static void Home()
    {
        Go(typeof(LibraryPage));
        Frame?.BackStack.Clear();
    }

    /// <summary>Shows the "update ready" dialog over the current page.</summary>
    public static void ShowUpdateDialog() => App.Window?.ShowUpdateDialog();

    /// <summary>Shows a short toast in the bottom-right corner.</summary>
    public static void Toast(string text) => App.Window?.ShowToast(text);

    /// <summary>Sets the text in the custom title bar (readers show the chapter).</summary>
    public static void SetTitle(string? title) => App.Window?.SetTitle(title);
}
