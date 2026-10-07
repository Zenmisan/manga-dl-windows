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
        };
    }

    private void OnViewProfile(object sender, RoutedEventArgs e) => Nav.Go(typeof(ProfilePage));

    private void OnSave(object sender, RoutedEventArgs e)
    {
        AppServices.Settings.Save();
        Nav.Toast("Profile settings saved");
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
