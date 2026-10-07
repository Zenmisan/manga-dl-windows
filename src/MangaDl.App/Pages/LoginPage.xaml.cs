using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages;

public sealed partial class LoginPage : Page
{
    public LoginPage() => InitializeComponent();

    // TODO(backend): authenticate, then go home.
    private void OnSignIn(object sender, RoutedEventArgs e) => Nav.Home();
    private void OnForgot(object sender, RoutedEventArgs e) => Nav.Go(typeof(ForgotPasswordPage));
    private void OnCreate(object sender, RoutedEventArgs e) => Nav.Go(typeof(RegisterPage));
}
