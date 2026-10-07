using MangaDl.Pages.Settings;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages;

public sealed partial class ProfilePage : Page
{
    public ProfilePage() => InitializeComponent();

    private void OnEdit(object sender, RoutedEventArgs e) => Nav.Go(typeof(SettingsPage), "Account");
}
