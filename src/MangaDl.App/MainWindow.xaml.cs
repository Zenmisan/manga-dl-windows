using System.Runtime.InteropServices;
using MangaDl.Pages;
using MangaDl.Pages.Settings;
using MangaDl.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Windows.Graphics;

namespace MangaDl;

public sealed partial class MainWindow : Window
{
    private readonly List<RadioButton> _navItems;
    private DispatcherTimer? _toastTimer;

    public MainWindow()
    {
        InitializeComponent();

        // Custom dark title bar; Windows keeps drawing the caption buttons.
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        var bar = AppWindow.TitleBar;
        bar.ButtonBackgroundColor = Colors.Transparent;
        bar.ButtonInactiveBackgroundColor = Colors.Transparent;
        bar.ButtonForegroundColor = ColorHelper.FromArgb(0xBF, 0xFF, 0xFF, 0xFF);
        bar.ButtonInactiveForegroundColor = ColorHelper.FromArgb(0x73, 0xFF, 0xFF, 0xFF);
        bar.ButtonHoverBackgroundColor = ColorHelper.FromArgb(0x1F, 0xFF, 0xFF, 0xFF);
        bar.ButtonHoverForegroundColor = Colors.White;
        bar.ButtonPressedBackgroundColor = ColorHelper.FromArgb(0x14, 0xFF, 0xFF, 0xFF);
        bar.ButtonPressedForegroundColor = Colors.White;
        AppWindow.SetIcon("Assets/AppIcon.ico");

        // 1440×900 at the monitor's scale, with a sensible minimum.
        var scale = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(1440 * scale), (int)(900 * scale)));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = (int)(1100 * scale);
            presenter.PreferredMinimumHeight = (int)(700 * scale);
        }

        _navItems = [NavLibrary, NavUpdates, NavHistory, NavBrowse, NavDownloads, NavStats, NavAllPages, NavNotifications, NavSettings, NavHelp];
#if DEBUG
        NavAllPages.Visibility = Visibility.Visible;
#endif

        Nav.Frame = ContentFrame;
        ContentFrame.Navigated += OnNavigated;
        Nav.Go(typeof(OnboardingPage));
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    private void OnNavigated(object sender, NavigationEventArgs e)
    {
        var type = e.SourcePageType;
        Sidebar.Visibility = Nav.ChromeFor(type) == Chrome.App ? Visibility.Visible : Visibility.Collapsed;

        Nav.SidebarKey.TryGetValue(type, out var key);
        foreach (var item in _navItems) item.IsChecked = (string)item.Tag == key;

        SetTitle(null);
        if (ContentFrame.Content is UIElement page) FadeIn(page);
    }

    private static void FadeIn(UIElement element)
    {
        var fade = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(fade, element);
        Storyboard.SetTargetProperty(fade, "Opacity");
        var board = new Storyboard();
        board.Children.Add(fade);
        board.Begin();
    }

    private void OnNavClick(object sender, RoutedEventArgs e)
    {
        var page = ((FrameworkElement)sender).Tag switch
        {
            "Library" => typeof(LibraryPage),
            "Updates" => typeof(UpdatesPage),
            "History" => typeof(HistoryPage),
            "Browse" => typeof(ExtensionsPage),
            "Downloads" => typeof(DownloadsPage),
            "Stats" => typeof(StatsPage),
            "Notifications" => typeof(NotificationsPage),
            "Settings" => typeof(SettingsPage),
            "Help" => typeof(HelpPage),
            "AllPages" => typeof(AllPagesPage),
            _ => typeof(LibraryPage),
        };
        if (ContentFrame.SourcePageType != page) Nav.Go(page);
    }

    private void OnProfileClick(object sender, RoutedEventArgs e) => Nav.Go(typeof(ProfilePage));

    public void SetTitle(string? title) => TitleText.Text = title ?? "manga-dl";

    // ----- Update dialog -----
    public void ShowUpdateDialog() => UpdateOverlay.Visibility = Visibility.Visible;
    private void OnUpdateLater(object sender, RoutedEventArgs e) => UpdateOverlay.Visibility = Visibility.Collapsed;
    private void OnScrimTapped(object sender, TappedRoutedEventArgs e) => UpdateOverlay.Visibility = Visibility.Collapsed;

    private void OnUpdateInstall(object sender, RoutedEventArgs e)
    {
        UpdateOverlay.Visibility = Visibility.Collapsed;
        ShowToast("Restarting to install the update");
    }

    // ----- Toast (auto-dismiss after 3.5s, per design.md) -----
    public void ShowToast(string text)
    {
        ToastText.Text = text;
        Toast.Visibility = Visibility.Visible;
        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(3500) };
        _toastTimer.Tick += (_, _) =>
        {
            Toast.Visibility = Visibility.Collapsed;
            _toastTimer?.Stop();
        };
        _toastTimer.Start();
    }

    private void OnToastClose(object sender, RoutedEventArgs e) => Toast.Visibility = Visibility.Collapsed;
}
