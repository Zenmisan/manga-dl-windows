using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages.Settings;

public sealed partial class AccountSettingsPage : Page
{
    public AccountSettingsPage() => InitializeComponent();

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

    private void OnConfirmSignOut(object sender, RoutedEventArgs e)
    {
        AppServices.Settings.ApiKey = null;
        AppServices.Settings.Save();
        Nav.Go(typeof(LoginPage));
    }
}
