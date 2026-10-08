using MangaDl.Controls;
using MangaDl.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace MangaDl.Pages;

public sealed partial class HelpPage : Page
{
    private const string SupportEmail = "zenmisan@gmail.com";

    private int _open;

    public HelpPage() => InitializeComponent();

    /// <summary>Accordion: one answer open at a time.</summary>
    private void OnToggle(object sender, RoutedEventArgs e)
    {
        var index = int.Parse((string)((FrameworkElement)sender).Tag);
        _open = _open == index ? -1 : index;
        var questions = new[] { Q0, Q1, Q2, Q3 };
        var answers = new[] { A0, A1, A2, A3 };
        var icons = new MdIcon[] { I0, I1, I2, I3 };
        for (var i = 0; i < answers.Length; i++)
        {
            var open = i == _open;
            answers[i].Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            questions[i].FontWeight = open ? FontWeights.Bold : FontWeights.SemiBold;
            icons[i].Kind = open ? "ChevronUp" : "ChevronDown";
        }
    }

    /// <summary>No support-ticket API exists on the backend — this opens the user's
    /// default mail client addressed to the same contact the web Landing page uses.</summary>
    private async void OnSend(object sender, RoutedEventArgs e)
    {
        var message = MessageBox.Text.Trim();
        if (message.Length == 0)
        {
            Nav.Toast("Write a message first");
            return;
        }

        var category = CatBug.IsChecked == true ? "Bug Report"
            : CatFeature.IsChecked == true ? "Feature Request"
            : CatAccount.IsChecked == true ? "Account"
            : CatSource.IsChecked == true ? "Source / Extension"
            : "General";

        var subject = Uri.EscapeDataString($"[manga-dl Windows] {category}");
        var body = Uri.EscapeDataString(message);
        var mailto = new Uri($"mailto:{SupportEmail}?subject={subject}&body={body}");

        try
        {
            var launched = await Launcher.LaunchUriAsync(mailto);
            if (launched)
            {
                MessageBox.Text = string.Empty;
                Nav.Toast("Opened in your email app");
            }
            else
            {
                Nav.Toast($"No email app found. Message us at {SupportEmail}");
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("HelpPage.OnSend", ex);
            Nav.Toast($"Couldn't open email app. Message us at {SupportEmail}");
        }
    }
}
