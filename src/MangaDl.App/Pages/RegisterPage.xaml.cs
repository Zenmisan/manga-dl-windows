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

    // TODO(backend): create the account.
    private void OnCreate(object sender, RoutedEventArgs e) => Nav.Home();
    private void OnSignIn(object sender, RoutedEventArgs e) => Nav.Go(typeof(LoginPage));
}
