using MangaDl.Core;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace MangaDl.Helpers;

/// <summary>
/// Small static functions used from x:Bind in XAML (e.g. {x:Bind h:X.Brush(Cover)}).
/// Keeps templates free of converters.
/// </summary>
public static class X
{
    public static Brush Res(string key) => (Brush)Application.Current.Resources[key];

    public static SolidColorBrush Hex(string hex) => new(ToColor(hex));

    public static Color ToColor(string hex)
    {
        var h = hex.TrimStart('#');
        if (h.Length == 6) h = "FF" + h;
        var v = Convert.ToUInt32(h, 16);
        return ColorHelper.FromArgb((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
    }

    public static Visibility Vis(bool show) => show ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility VisNot(bool hide) => hide ? Visibility.Collapsed : Visibility.Visible;
    public static Visibility VisN(int n) => n > 0 ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility VisStr(string? s) => string.IsNullOrEmpty(s) ? Visibility.Collapsed : Visibility.Visible;

    public static string Count(int n) => n.ToString();
    public static string Upper(string s) => s.ToUpperInvariant();
    public static double ReadOpacity(bool read) => read ? 0.5 : 1.0;
    public static double InLibraryOpacity(bool inLibrary) => inLibrary ? 0.55 : 1.0;

    // Notifications
    public static Brush NoticeIconBg(NoticeKind k) => k switch
    {
        NoticeKind.Error => Res("ErrorSoftBrush"),
        NoticeKind.Success => Res("SuccessSoftBrush"),
        _ => Res("SurfaceHighBrush"),
    };

    public static Brush NoticeIconFg(NoticeKind k) => k switch
    {
        NoticeKind.Error => Res("ErrorTextBrush"),
        NoticeKind.Success => Res("SuccessTextBrush"),
        _ => Res("FgBrush"),
    };

    public static Brush UnreadRow(bool unread) => unread ? Res("AccentWashBrush") : new SolidColorBrush(Colors.Transparent);

    // Downloads
    public static Brush DownloadText(DownloadState s) => s switch
    {
        DownloadState.Failed => Res("ErrorTextBrush"),
        DownloadState.Done => Res("SuccessTextBrush"),
        _ => Res("FgMutedBrush"),
    };

    public static Brush DownloadBar(DownloadState s) => s switch
    {
        DownloadState.Failed => Res("ErrorBarBrush"),
        DownloadState.Done => Res("SuccessBrush"),
        DownloadState.Paused => Res("FgFaintBrush"),
        _ => Res("AccentBrush"),
    };

    public static string DownloadAction(DownloadState s) => s switch
    {
        DownloadState.Failed => "Retry",
        DownloadState.Paused => "Resume",
        DownloadState.Done => "Remove",
        _ => "Pause",
    };

    public static string DownloadIcon(DownloadState s) => s switch
    {
        DownloadState.Failed => "Refresh",
        DownloadState.Paused => "Play",
        DownloadState.Done => "Close",
        _ => "Pause",
    };

    // Extensions table
    public static string ExtStatus(ExtensionState s) => s switch
    {
        ExtensionState.UpdateAvailable => "Update available",
        ExtensionState.Installed => "Installed",
        ExtensionState.Broken => "Failed to load",
        _ => "Not installed",
    };

    public static Brush ExtStatusColor(ExtensionState s) => s switch
    {
        ExtensionState.UpdateAvailable => Res("AccentSoftBrush"),
        ExtensionState.Installed => Res("SuccessTextBrush"),
        ExtensionState.Broken => Res("ErrorTextBrush"),
        _ => Res("FgSubtleBrush"),
    };

    public static string ExtAction(ExtensionState s) => s switch
    {
        ExtensionState.UpdateAvailable => "Update",
        ExtensionState.Installed => "Uninstall",
        ExtensionState.Broken => "Reinstall",
        _ => "Install",
    };

    public static Style ExtActionStyle(ExtensionState s) => (Style)Application.Current.Resources[s switch
    {
        ExtensionState.UpdateAvailable => "SmallPrimaryButton",
        ExtensionState.Broken => "SmallDangerButton",
        _ => "SmallGhostButton",
    }];

    // Chapters table
    public static Brush ChapterStatus(string status) =>
        status.StartsWith("Page", StringComparison.Ordinal) ? Res("AccentLightBrush") : Res("FgSoftBrush");

    // Import
    public static Brush ImportText(string kind) => kind switch
    {
        "done" => Res("SuccessTextBrush"),
        "error" => Res("ErrorTextBrush"),
        _ => Res("FgMutedBrush"),
    };

    public static Brush ImportBar(string kind) => kind switch
    {
        "done" => Res("SuccessBrush"),
        "error" => Res("ErrorBarBrush"),
        "queued" => new SolidColorBrush(Colors.Transparent),
        _ => Res("AccentBrush"),
    };

    // Trackers
    public static string TrackerState(bool connected) => connected ? "Signed in as [username]" : "Not connected";
    public static Brush TrackerStateColor(bool connected) => connected ? Res("SuccessTextBrush") : Res("FgSubtleBrush");
    public static Brush TrackerBorder(bool connected) => connected ? Res("SuccessLineBrush") : Res("DividerStrongBrush");
    public static string TrackerAction(bool connected) => connected ? "Log Out" : "Connect";
    public static Style TrackerActionStyle(bool connected) =>
        (Style)Application.Current.Resources[connected ? "SmallGhostButton" : "SmallPrimaryButton"];

    // Heatmap
    public static Brush Heat(int level) => level switch
    {
        3 => Res("AccentBrush"),
        2 => new SolidColorBrush(ColorHelper.FromArgb(0xA6, 0xDC, 0x26, 0x26)),
        1 => new SolidColorBrush(ColorHelper.FromArgb(0x59, 0xDC, 0x26, 0x26)),
        _ => Res("Surface4Brush"),
    };
}
