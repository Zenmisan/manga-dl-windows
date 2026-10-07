using MangaDl.Core.Auth;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages;

public sealed partial class ForgotPasswordPage : Page
{
    public ForgotPasswordPage() => InitializeComponent();

    private async void OnSend(object sender, RoutedEventArgs e)
    {
        var email = EmailBox.Text.Trim();
        if (email.Length == 0)
        {
            ShowError("Enter your email address.");
            return;
        }

        if (!AppServices.Settings.HasSupabase)
        {
            SendButton.Visibility = Visibility.Collapsed;
            SentState.Visibility = Visibility.Visible;
            return;
        }

        ErrorText.Visibility = Visibility.Collapsed;
        SendButton.IsEnabled = false;
        try
        {
            await AppServices.Auth.RequestPasswordResetAsync(email);
            SendButton.Visibility = Visibility.Collapsed;
            SentState.Visibility = Visibility.Visible;
        }
        catch (AuthException ex)
        {
            ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            AppLog.Warn("ForgotPasswordPage.OnSend", ex);
            ShowError("Couldn't reach the server. Check your connection.");
        }
        finally
        {
            SendButton.IsEnabled = true;
        }
    }

    private void OnBack(object sender, RoutedEventArgs e) => Nav.Back();

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
