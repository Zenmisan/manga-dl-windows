using System.Text.Json;
using MangaDl.Helpers;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages.Settings;

public sealed partial class AccountSettingsPage : Page
{
    public AccountSettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var email = AppServices.Settings.UserEmail;
            EmailRow.Description = email ?? "Not signed in";
            if (!string.IsNullOrEmpty(email))
            {
                UsernameEmailText.Text = email;
                var prefix = email.Contains('@') ? email[..email.IndexOf('@')] : email;
                ProfileNameText.Text = char.ToUpperInvariant(prefix[0]) + prefix[1..];
            }
            else
            {
                UsernameEmailText.Text = "Guest / Offline mode";
                ProfileNameText.Text = "Local Reader";
            }

            DisplayNameBox.Text = AppServices.Settings.DisplayName ?? "";
            BioBox.Text = AppServices.Settings.Bio ?? "";
            AvatarUrlBox.Text = AppServices.Settings.AvatarUrl ?? "";
            BackendUrlBox.Text = AppServices.Settings.BackendUrl ?? "";
        };
    }

    private void OnViewProfile(object sender, RoutedEventArgs e) => Nav.Go(typeof(ProfilePage));

    private void OnAvatarUrlChanged(object sender, TextChangedEventArgs e)
    {
        var url = AvatarUrlBox.Text.Trim();
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
        {
            try
            {
                AvatarPreview.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(uri);
            }
            catch (Exception ex)
            {
                AppLog.Warn("AccountSettingsPage.OnAvatarUrlChanged", ex);
                AvatarPreview.Source = null;
            }
        }
        else
        {
            AvatarPreview.Source = null;
        }
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        // Local-first, same pattern as the rest of AppSettings — the app must stay
        // fully usable with no backend configured (PROJECT.md's own rule for the
        // native-app architecture). Backend sync below is a best-effort bonus.
        AppServices.Settings.DisplayName = DisplayNameBox.Text.Trim();
        AppServices.Settings.Bio = BioBox.Text.Trim();
        AppServices.Settings.AvatarUrl = AvatarUrlBox.Text.Trim();
        AppServices.Settings.BackendUrl = BackendUrlBox.Text.Trim();
        AppServices.Settings.Save();
        AppServices.Http.BackendUrl = AppServices.Settings.BackendUrl ?? string.Empty;

        if (string.IsNullOrWhiteSpace(AppServices.Settings.BackendUrl))
        {
            Nav.Toast("Profile saved on this PC");
            return;
        }

        var (accessToken, _) = CredentialStore.Load();
        if (string.IsNullOrEmpty(accessToken))
        {
            Nav.Toast("Profile saved on this PC (sign in to sync to your account)");
            return;
        }

        try
        {
            var body = JsonSerializer.Serialize(new
            {
                display_name = AppServices.Settings.DisplayName,
                bio = AppServices.Settings.Bio,
                avatar_url = AppServices.Settings.AvatarUrl
            });
            var headers = new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json",
                ["Authorization"] = $"Bearer {accessToken}"
            };
            var result = await AppServices.Http.FetchAsync("/users/profile", "PUT", headers, body);
            Nav.Toast(result.IsSuccess ? "Profile saved and synced" : "Profile saved on this PC (sync failed)");
        }
        catch (Exception ex)
        {
            AppLog.Warn("AccountSettingsPage.OnSave", ex);
            Nav.Toast("Profile saved on this PC (sync failed)");
        }
    }

    private async void OnTestBackend(object sender, RoutedEventArgs e)
    {
        var url = BackendUrlBox.Text.Trim();
        if (url.Length == 0)
        {
            ShowBackendStatus("Enter a server URL first.", success: false);
            return;
        }

        try
        {
            var result = await AppServices.Http.FetchAsync(url.TrimEnd('/') + "/health");
            ShowBackendStatus(result.IsSuccess ? "Connected." : $"Server responded with {result.StatusCode}.", result.IsSuccess);
        }
        catch (Exception ex)
        {
            AppLog.Warn("AccountSettingsPage.OnTestBackend", ex);
            ShowBackendStatus("Couldn't reach that server.", success: false);
        }
    }

    private void ShowBackendStatus(string message, bool success)
    {
        BackendStatusText.Text = message;
        BackendStatusText.Foreground = X.Res(success ? "SuccessTextBrush" : "ErrorTextBrush");
        BackendStatusText.Visibility = Visibility.Visible;
    }

    private async void OnSyncNow(object sender, RoutedEventArgs e)
    {
        if (SyncButton != null) SyncButton.IsEnabled = false;
        if (SyncStatusText != null) SyncStatusText.Text = "Syncing with cloud...";

        try
        {
            var result = await AppServices.Sync.SyncAllAsync();
            if (result.Success)
            {
                var time = DateTime.Now.ToString("h:mm tt");
                if (SyncStatusText != null)
                    SyncStatusText.Text = $"Last synced {time} · {result.PulledLibraryCount} pulled, {result.PushedLibraryCount} pushed";
                Nav.Toast($"Sync complete: {result.PulledLibraryCount} titles, {result.PulledProgressCount} progress records");
            }
            else
            {
                if (SyncStatusText != null)
                    SyncStatusText.Text = "Sync failed: " + (result.ErrorMessage ?? "Error");
                Nav.Toast(result.ErrorMessage ?? "Cloud sync failed. Check connection.");
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("AccountSettingsPage.OnSyncNow", ex);
            if (SyncStatusText != null) SyncStatusText.Text = "Sync failed";
            Nav.Toast("Cloud sync failed. Check connection.");
        }
        finally
        {
            if (SyncButton != null) SyncButton.IsEnabled = true;
        }
    }

    private void OnManageDevices(object sender, RoutedEventArgs e) =>
        Nav.Toast("Current device: Windows PC (Active)");

    private void OnChangeEmail(object sender, RoutedEventArgs e) =>
        Nav.Toast("Verification email sent to update email address");

    private void OnChangePassword(object sender, RoutedEventArgs e) =>
        Nav.Go(typeof(ForgotPasswordPage));

    private void OnSignOut(object sender, RoutedEventArgs e) => SignOutConfirm.Visibility = Visibility.Visible;

    private void OnCancelSignOut(object sender, RoutedEventArgs e) => SignOutConfirm.Visibility = Visibility.Collapsed;

    private async void OnConfirmSignOut(object sender, RoutedEventArgs e)
    {
        var (accessToken, _) = CredentialStore.Load();
        if (!string.IsNullOrEmpty(accessToken))
        {
            await AppServices.Auth.SignOutAsync(accessToken);
        }
        CredentialStore.Clear();

        AppServices.Settings.ApiKey = null;
        AppServices.Settings.UserEmail = null;
        AppServices.Settings.UserId = null;
        AppServices.Settings.Save();
        Nav.Go(typeof(LoginPage));
    }
}
