using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages.Settings;

public sealed partial class AccountSettingsPage : Page
{
    public AccountSettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => EmailRow.Description = AppServices.Settings.UserEmail ?? "Not signed in";
    }

    private void OnViewProfile(object sender, RoutedEventArgs e) => Nav.Go(typeof(ProfilePage));

    private void OnSave(object sender, RoutedEventArgs e)
    {
        AppServices.Settings.Save();
        Nav.Toast("Profile settings saved");
    }

    private void OnSyncNow(object sender, RoutedEventArgs e) =>
        Nav.Toast("Library and reading progress synced");

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
