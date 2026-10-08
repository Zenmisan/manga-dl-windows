using MangaDl.Core.Auth;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages;

public sealed partial class LoginPage : Page
{
    private bool _googleAuthorizing;

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
            _ = Task.Run(async () =>
            {
                try { await AppServices.Sync.SyncAllAsync(); }
                catch (Exception syncEx) { AppLog.Warn("LoginPage.PostSignInSync", syncEx); }
            });
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

    private async void OnGoogleSignIn(object sender, RoutedEventArgs e)
    {
        if (!AppServices.Settings.HasSupabase)
        {
            Nav.Home();
            return;
        }

        if (_googleAuthorizing)
        {
            Nav.Toast("An authorization is already in progress. Please check your browser.");
            return;
        }

        _googleAuthorizing = true;
        ErrorText.Visibility = Visibility.Collapsed;
        GoogleButton.IsEnabled = false;
        try
        {
            Nav.Toast("Opening browser for Google sign-in...");
            var (verifier, challenge) = SupabaseAuthService.GeneratePkceS256();
            var authUrl = AppServices.Auth.GetOAuthAuthorizeUrl("google", AppServices.Settings.GoogleRedirectUri, challenge);

            var listenTask = AppServices.Loopback.WaitForAuthCodeAsync(TimeSpan.FromSeconds(120));
            await Windows.System.Launcher.LaunchUriAsync(new Uri(authUrl));

            var code = await listenTask;
            if (string.IsNullOrEmpty(code))
            {
                ShowError("Google sign-in timed out or was cancelled.");
                return;
            }

            var session = await AppServices.Auth.ExchangeOAuthCodeAsync(code, verifier);
            CredentialStore.Save(session.AccessToken, session.RefreshToken);
            AppServices.Settings.UserEmail = session.Email;
            AppServices.Settings.UserId = session.UserId;
            AppServices.Settings.Save();
            _ = Task.Run(async () =>
            {
                try { await AppServices.Sync.SyncAllAsync(); }
                catch (Exception syncEx) { AppLog.Warn("LoginPage.PostGoogleSignInSync", syncEx); }
            });
            Nav.Home();
        }
        catch (AuthException ex)
        {
            ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            AppLog.Warn("LoginPage.OnGoogleSignIn", ex);
            ShowError("Google sign-in failed. Check your connection.");
        }
        finally
        {
            _googleAuthorizing = false;
            GoogleButton.IsEnabled = true;
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
