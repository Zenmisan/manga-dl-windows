using MangaDl.Core.Auth;
using MangaDl.Helpers;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages;

public sealed partial class RegisterPage : Page
{
    public RegisterPage() => InitializeComponent();

    private void OnPasswordChanged(object sender, RoutedEventArgs e) => Validate();
    private void OnTermsChanged(object sender, RoutedEventArgs e) => Validate();

    /// <summary>UI-only validation so the error and strength states can be seen. Replace with real rules.</summary>
    private void Validate()
    {
        if (Password is null || Confirm is null || Terms is null || StrengthText is null || CreateButton is null || Strength4 is null) return;
        var pw = Password.Password;
        var match = pw == Confirm.Password;
        var score = Math.Min(4, (pw.Length >= 8 ? 1 : 0) + (pw.Any(char.IsDigit) ? 1 : 0) + (pw.Any(char.IsUpper) ? 1 : 0) + (pw.Any(ch => !char.IsLetterOrDigit(ch)) ? 1 : 0) + (pw.Length >= 6 ? 1 : 0));
        var bars = new[] { Strength1, Strength2, Strength3, Strength4 };
        for (var i = 0; i < bars.Length; i++)
            bars[i].Background = X.Res(i < score ? (match ? "SuccessBrush" : "ErrorBarBrush") : "LineBrush");

        StrengthText.Text = match ? (score >= 3 ? "Strong password · passwords match" : "Weak password · passwords match") : "Passwords don't match.";
        StrengthText.Foreground = X.Res(match ? "SuccessTextBrush" : "ErrorTextBrush");
        Confirm.Style = (Style)Application.Current.Resources[match ? "MdPasswordBox" : "MdPasswordBoxError"];
        CreateButton.IsEnabled = match && pw.Length > 0 && Terms.IsChecked == true;
    }

    private async void OnCreate(object sender, RoutedEventArgs e)
    {
        if (!AppServices.Settings.HasSupabase)
        {
            Nav.Home();
            return;
        }

        var username = UsernameBox.Text.Trim();
        var email = EmailBox.Text.Trim();
        var password = Password.Password;

        ErrorText.Visibility = Visibility.Collapsed;
        CreateButton.IsEnabled = false;
        try
        {
            var result = await AppServices.Auth.SignUpAsync(username, email, password);
            if (result.Session is { } session)
            {
                CredentialStore.Save(session.AccessToken, session.RefreshToken);
                AppServices.Settings.UserEmail = session.Email ?? email;
                AppServices.Settings.UserId = session.UserId;
                AppServices.Settings.Save();
                Nav.Home();
            }
            else
            {
                ShowError(result.Message ?? "Check your email to confirm your account.");
            }
        }
        catch (AuthException ex)
        {
            ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            AppLog.Warn("RegisterPage.OnCreate", ex);
            ShowError("Couldn't reach the server. Check your connection.");
        }
        finally
        {
            Validate();
        }
    }

    private void OnSignIn(object sender, RoutedEventArgs e) => Nav.Go(typeof(LoginPage));

    private void OnTermsClick(object sender, RoutedEventArgs e) =>
        Nav.Toast("Terms of service: Free and open-source software under MIT.");

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
