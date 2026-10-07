using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages;

/// <summary>First-run step 2 of 3: connect to the optional backend.</summary>
public sealed partial class OnboardingPage : Page
{
    public OnboardingPage() => InitializeComponent();

    private async void OnTest(object sender, RoutedEventArgs e)
    {
        try
        {
            Connected.Visibility = Visibility.Visible;
            var ping = await AppServices.Http.FetchAsync("https://api.mangadex.org/ping");
            Nav.Toast(ping.IsSuccess ? "Connection test successful" : "Server reached");
        }
        catch
        {
            Nav.Toast("Connection failed");
        }
    }

    private void OnSkip(object sender, RoutedEventArgs e) => Nav.Home();
    private void OnBack(object sender, RoutedEventArgs e) => Nav.Back();
    private void OnContinue(object sender, RoutedEventArgs e) => Nav.Go(typeof(LoginPage));
}
