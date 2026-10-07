using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages;

public sealed partial class ForgotPasswordPage : Page
{
    public ForgotPasswordPage() => InitializeComponent();

    // TODO(backend): request the reset email.
    private void OnSend(object sender, RoutedEventArgs e)
    {
        SendButton.Visibility = Visibility.Collapsed;
        SentState.Visibility = Visibility.Visible;
    }

    private void OnBack(object sender, RoutedEventArgs e) => Nav.Back();
}
