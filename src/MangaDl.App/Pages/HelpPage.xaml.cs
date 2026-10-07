using MangaDl.Controls;
using MangaDl.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages;

public sealed partial class HelpPage : Page
{
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

    // TODO(backend): send the support message.
    private void OnSend(object sender, RoutedEventArgs e) => Nav.Toast("Message sent — we'll reply by email");
}
