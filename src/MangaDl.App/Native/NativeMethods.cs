using System.Runtime.InteropServices;

namespace MangaDl.Native;

/// <summary>
/// P/Invoke surface for MangaDl.Native.dll (the C++ project). Not called by the UI yet.
/// To ship the DLL with the app, build MangaDl.Native for the same platform and add it
/// as Content to MangaDl.App (see README).
/// </summary>
internal static partial class NativeMethods
{
    private const string Dll = "MangaDl.Native.dll";

    [LibraryImport(Dll, EntryPoint = "mdl_version")]
    internal static partial uint Version();

    [LibraryImport(Dll, EntryPoint = "mdl_detect_borders")]
    internal static unsafe partial int DetectBorders(byte* pixels, int width, int height, int stride,
        out int x, out int y, out int w, out int h);
}
