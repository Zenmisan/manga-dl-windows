using MangaDl.Core;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Pages;

public sealed partial class NotificationsPage : Page
{
    public IReadOnlyList<Notice> Items { get; } = Sample.Notices;

    public NotificationsPage() => InitializeComponent();
}
