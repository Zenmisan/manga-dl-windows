using Microsoft.UI.Xaml;

namespace MangaDl;

public partial class App : Application
{
    public static MainWindow? Window { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            await MangaDl.Services.AppServices.InitializeAsync();
        }
        catch (Exception ex)
        {
            MangaDl.Services.AppLog.Warn("AppServices.InitializeAsync", ex);
        }

        Window = new MainWindow();
        Window.Activate();
    }
}
