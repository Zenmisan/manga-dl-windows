using MangaDl.Pages.Settings;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages;

public sealed partial class AllPagesPage : Page
{
    public AllPagesPage() => InitializeComponent();

    private void OnOpen(object sender, RoutedEventArgs e)
    {
        var tag = (string)((FrameworkElement)sender).Tag;
        if (tag.StartsWith("Set:", StringComparison.Ordinal))
        {
            Nav.Go(typeof(SettingsPage), tag[4..]);
            return;
        }

        switch (tag)
        {
            case "Onboarding": Nav.Go(typeof(OnboardingPage)); break;
            case "Login": Nav.Go(typeof(LoginPage)); break;
            case "Register": Nav.Go(typeof(RegisterPage)); break;
            case "Forgot": Nav.Go(typeof(ForgotPasswordPage)); break;
            case "Library": Nav.Go(typeof(LibraryPage)); break;
            case "Updates": Nav.Go(typeof(UpdatesPage)); break;
            case "History": Nav.Go(typeof(HistoryPage)); break;
            case "Notifications": Nav.Go(typeof(NotificationsPage)); break;
            case "Downloads": Nav.Go(typeof(DownloadsPage)); break;
            case "Stats": Nav.Go(typeof(StatsPage)); break;
            case "Extensions": Nav.Go(typeof(ExtensionsPage)); break;
            case "Sources": Nav.Go(typeof(ExtensionsPage), "sources"); break;
            case "Migrate": Nav.Go(typeof(ExtensionsPage), "migrate"); break;
            case "Search": Nav.Go(typeof(SearchPage)); break;
            case "Source": Nav.Go(typeof(BrowseSourcePage), "MangaDex"); break;
            case "Import": Nav.Go(typeof(ImportPage)); break;
            case "Detail": Nav.Go(typeof(MangaDetailPage)); break;
            case "Reader": Nav.Go(typeof(ReaderPage)); break;
            case "ReaderSingle": Nav.Go(typeof(ReaderPage), "single"); break;
            case "Novel": Nav.Go(typeof(NovelReaderPage)); break;
            case "Profile": Nav.Go(typeof(ProfilePage)); break;
            case "Help": Nav.Go(typeof(HelpPage)); break;
            case "Update": Nav.ShowUpdateDialog(); break;
            case "Toast": Nav.Toast("4 chapters downloaded"); break;
        }
    }
}
