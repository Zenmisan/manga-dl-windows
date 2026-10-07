using System.Diagnostics;

namespace MangaDl.Services;

/// <summary>Minimal dev-visibility logging. Not an observability pipeline —
/// just makes swallowed exceptions show up somewhere instead of nowhere.</summary>
public static class AppLog
{
    public static void Warn(string context, Exception ex) =>
        Debug.WriteLine($"[WARN] {context}: {ex}");
}
