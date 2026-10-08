using System.Collections.ObjectModel;
using System.Diagnostics;
using MangaDl.Core;
using MangaDl.Core.Tracking;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages.Settings;

public sealed partial class TrackersSettingsPage : Page
{
    private bool _authorizing;
    public ObservableCollection<Tracker> Trackers { get; } = new();

    public TrackersSettingsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadTrackers();
        AutoSyncSwitch.IsChecked = AppServices.Settings.AutoSyncTrackers;
        MarkReadingSwitch.IsChecked = AppServices.Settings.MarkTrackerReadingOnFirstChapter;
        MarkCompletedSwitch.IsChecked = AppServices.Settings.MarkTrackerCompletedOnFinish;
        PullProgressSwitch.IsChecked = AppServices.Settings.PullProgressFromTrackers;
    }

    private void LoadTrackers()
    {
        Trackers.Clear();
        Trackers.Add(new Tracker(
            "AniList",
            "AL",
            "#1E3A5F",
            "Sync status, score, chapters read and dates.",
            Connected: AppServices.Settings.AnilistConnected,
            Username: AppServices.Settings.AnilistUsername));

        Trackers.Add(new Tracker(
            "MyAnimeList",
            "MAL",
            "#1F2C4F",
            "Sync manga status with MyAnimeList.",
            Connected: AppServices.Settings.MalConnected,
            Username: AppServices.Settings.MalUsername));

        Trackers.Add(new Tracker("Kitsu", "K", "#3B1F2E", "Sync manga progress with Kitsu."));
        Trackers.Add(new Tracker("MangaUpdates", "MU", "#2E2412", "Keep your MangaUpdates lists current."));
        Trackers.Add(new Tracker("Shikimori", "S", "#22222A", "Sync progress with Shikimori."));
        Trackers.Add(new Tracker("Bangumi", "B", "#311A1F", "Sync progress with Bangumi."));
    }

    private void OnSyncToggleClicked(object sender, RoutedEventArgs e)
    {
        AppServices.Settings.AutoSyncTrackers = AutoSyncSwitch.IsChecked == true;
        AppServices.Settings.MarkTrackerReadingOnFirstChapter = MarkReadingSwitch.IsChecked == true;
        AppServices.Settings.MarkTrackerCompletedOnFinish = MarkCompletedSwitch.IsChecked == true;
        AppServices.Settings.PullProgressFromTrackers = PullProgressSwitch.IsChecked == true;
        AppServices.Settings.Save();
    }

    private async void OnTrackerAction(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not Tracker tracker)
            return;

        var idx = Trackers.IndexOf(tracker);
        if (idx < 0) return;

        if (tracker.Name == "AniList")
        {
            await HandleAnilistActionAsync(tracker, idx);
        }
        else if (tracker.Name == "MyAnimeList")
        {
            await HandleMalActionAsync(tracker, idx);
        }
        else
        {
            Nav.Toast($"{tracker.Name} tracking will be available in an upcoming update.");
        }
    }

    private async Task HandleAnilistActionAsync(Tracker tracker, int idx)
    {
        if (tracker.Connected)
        {
            // Disconnect
            CredentialStore.ClearTrackerToken("anilist_token");
            CredentialStore.ClearTrackerToken("anilist_refresh_token");
            AppServices.Settings.AnilistConnected = false;
            AppServices.Settings.AnilistUsername = null;
            AppServices.Settings.Save();

            Trackers[idx] = tracker with { Connected = false, Username = null };
            Nav.Toast("Disconnected from AniList");
            return;
        }

        if (_authorizing)
        {
            Nav.Toast("An authorization is already in progress. Please check your browser.");
            return;
        }

        _authorizing = true;
        try
        {
            Nav.Toast("Opening browser for AniList authorization...");
            var authUrl = TrackerService.GetAnilistAuthUrl(
                AppServices.Settings.AnilistClientId,
                AppServices.Settings.AnilistRedirectUri);

            var listenTask = AppServices.Loopback.WaitForAuthCodeAsync(TimeSpan.FromSeconds(120));
            await OpenBrowserAsync(authUrl);

            var code = await listenTask;
            if (string.IsNullOrEmpty(code))
            {
                Nav.Toast("AniList authorization timed out or was cancelled.");
                return;
            }

            Nav.Toast("Authorizing AniList account...");
            var result = await AppServices.Trackers.ExchangeAnilistTokenAsync(
                code,
                AppServices.Settings.AnilistClientId,
                AppServices.Settings.AnilistClientSecret,
                AppServices.Settings.AnilistRedirectUri);

            if (result != null && !string.IsNullOrEmpty(result.AccessToken))
            {
                CredentialStore.SaveTrackerToken("anilist_token", result.AccessToken);
                if (!string.IsNullOrEmpty(result.RefreshToken))
                {
                    CredentialStore.SaveTrackerToken("anilist_refresh_token", result.RefreshToken);
                }
                AppServices.Settings.AnilistConnected = true;
                AppServices.Settings.AnilistUsername = result.Username;
                AppServices.Settings.Save();

                Trackers[idx] = tracker with { Connected = true, Username = result.Username };
                Nav.Toast($"Connected to AniList as @{result.Username ?? "user"}");
            }
            else
            {
                Nav.Toast("AniList authorization failed. Check your network or credentials.");
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("TrackersSettingsPage.HandleAnilistActionAsync", ex);
            Nav.Toast("Failed to connect to AniList");
        }
        finally
        {
            _authorizing = false;
        }
    }

    private async Task HandleMalActionAsync(Tracker tracker, int idx)
    {
        if (tracker.Connected)
        {
            // Disconnect
            CredentialStore.ClearTrackerToken("mal_token");
            CredentialStore.ClearTrackerToken("mal_refresh_token");
            AppServices.Settings.MalConnected = false;
            AppServices.Settings.MalUsername = null;
            AppServices.Settings.Save();

            Trackers[idx] = tracker with { Connected = false, Username = null };
            Nav.Toast("Disconnected from MyAnimeList");
            return;
        }

        if (_authorizing)
        {
            Nav.Toast("An authorization is already in progress. Please check your browser.");
            return;
        }

        _authorizing = true;
        try
        {
            Nav.Toast("Opening browser for MyAnimeList authorization...");
            var (verifier, _) = TrackerService.GeneratePkce();
            var authUrl = TrackerService.GetMalAuthUrl(
                AppServices.Settings.MalClientId,
                verifier,
                AppServices.Settings.MalRedirectUri);

            var listenTask = AppServices.Loopback.WaitForAuthCodeAsync(TimeSpan.FromSeconds(120));
            await OpenBrowserAsync(authUrl);

            var code = await listenTask;
            if (string.IsNullOrEmpty(code))
            {
                Nav.Toast("MyAnimeList authorization timed out or was cancelled.");
                return;
            }

            Nav.Toast("Authorizing MyAnimeList account...");
            var result = await AppServices.Trackers.ExchangeMalTokenAsync(
                code,
                verifier,
                AppServices.Settings.MalClientId,
                AppServices.Settings.MalRedirectUri);

            if (result != null && !string.IsNullOrEmpty(result.AccessToken))
            {
                CredentialStore.SaveTrackerToken("mal_token", result.AccessToken);
                if (!string.IsNullOrEmpty(result.RefreshToken))
                {
                    CredentialStore.SaveTrackerToken("mal_refresh_token", result.RefreshToken);
                }
                AppServices.Settings.MalConnected = true;
                AppServices.Settings.MalUsername = result.Username;
                AppServices.Settings.Save();

                Trackers[idx] = tracker with { Connected = true, Username = result.Username };
                Nav.Toast($"Connected to MyAnimeList as @{result.Username ?? "user"}");
            }
            else
            {
                Nav.Toast("MyAnimeList authorization failed.");
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("TrackersSettingsPage.HandleMalActionAsync", ex);
            Nav.Toast("Failed to connect to MyAnimeList");
        }
        finally
        {
            _authorizing = false;
        }
    }

    private static async Task OpenBrowserAsync(string url)
    {
        try
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri(url));
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppLog.Warn("TrackersSettingsPage.OpenBrowserAsync", ex);
            }
        }
    }
}
