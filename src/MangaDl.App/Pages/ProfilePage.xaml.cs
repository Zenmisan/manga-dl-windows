using MangaDl.Pages.Settings;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages;

public sealed partial class ProfilePage : Page
{
    public ProfilePage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadProfileAsync();
    }

    private async Task LoadProfileAsync()
    {
        var email = AppServices.Settings.UserEmail;
        if (!string.IsNullOrEmpty(email))
        {
            var name = email.Split('@')[0];
            if (DisplayNameText != null) DisplayNameText.Text = char.ToUpperInvariant(name[0]) + name[1..];
            if (UsernameText != null) UsernameText.Text = $"@{name} · Active reader";
        }
        else
        {
            if (DisplayNameText != null) DisplayNameText.Text = "Reader";
            if (UsernameText != null) UsernameText.Text = "@reader · Reading offline";
        }

        try
        {
            var lib = await AppServices.Database.GetLibraryAsync();
            var hist = await AppServices.Database.GetHistoryAsync();
            if (LibraryCountText != null) LibraryCountText.Text = lib.Count.ToString();
            if (ChaptersCountText != null) ChaptersCountText.Text = hist.Count.ToString();
            if (StreakCountText != null) StreakCountText.Text = "5";
        }
        catch (Exception ex)
        {
            AppLog.Warn("ProfilePage.LoadProfileAsync", ex);
        }
    }

    private void OnShare(object sender, RoutedEventArgs e) =>
        Nav.Toast("Profile link copied to clipboard");

    private void OnEdit(object sender, RoutedEventArgs e) => Nav.Go(typeof(SettingsPage), "Account");
}
