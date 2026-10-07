using MangaDl.Core.Auth;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages;

public sealed partial class LoginPage : Page
{
    public LoginPage() => InitializeComponent();

    private async void OnSignIn(object sender, RoutedEventArgs e)
    {
        if (!AppServices.Settings.HasSupabase)
        {
            Nav.Home();
            return;
        }

        var email = EmailBox.Text.Trim();
        var password = PasswordBox.Password;
        if (email.Length == 0 || password.Length == 0)
        {
            ShowError("Email and password required.");
            return;
        }

        ErrorText.Visibility = Visibility.Collapsed;
        SignInButton.IsEnabled = false;
        try
        {
            var session = await AppServices.Auth.SignInWithPasswordAsync(email, password);
            CredentialStore.Save(session.AccessToken, session.RefreshToken);
            AppServices.Settings.UserEmail = session.Email ?? email;
            AppServices.Settings.UserId = session.UserId;
            AppServices.Settings.Save();
            Nav.Home();
        }
        catch (AuthException ex)
        {
            ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            AppLog.Warn("LoginPage.OnSignIn", ex);
            ShowError("Couldn't reach the server. Check your connection.");
        }
        finally
        {
            SignInButton.IsEnabled = true;
        }
    }

    private void OnGuest(object sender, RoutedEventArgs e) => Nav.Home();

    private void OnForgot(object sender, RoutedEventArgs e) => Nav.Go(typeof(ForgotPasswordPage));
    private void OnCreate(object sender, RoutedEventArgs e) => Nav.Go(typeof(RegisterPage));

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
