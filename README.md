# manga-dl for Windows (WinUI 3)

A native Windows UI for manga-dl, built from the design canvas. It contains
**the screens only**. Every list is filled from `MangaDl.Core/SampleData.cs`,
and every button that would talk to a backend has a `TODO(backend)` comment.

## Requirements

- Windows 10 1809 (build 17763) or later. Windows 11 is recommended for development.
- Visual Studio 2022 17.12+ (or VS 2026) with these workloads:
  - **.NET desktop development**, plus the **Windows App SDK C# Templates** component
  - **Desktop development with C++**, for `MangaDl.Native`
- .NET 10 SDK
- Developer Mode turned on (Settings → System → For developers) to deploy the MSIX locally

## Build and run

1. Open `MangaDl.sln`.
2. Choose the platform: `x64`, or `ARM64` on an ARM PC. *Any CPU* is not supported by WinUI.
3. Set **MangaDl.App** as the startup project. Its launch profile is *MangaDl.App (Package)*.
4. Press F5.

The app opens on Onboarding. In Debug builds the sidebar has an **All pages** entry
that jumps straight to any screen, settings section, the update dialog or the toast.

## Layout

```
MangaDl.sln
src/
  MangaDl.Core/          net10.0 class library: models + sample data (no UI)
  MangaDl.App/           WinUI 3 app, single-project MSIX
    Themes/Theme.xaml    every colour, font, text style and control style (design tokens)
    App.xaml             merges Theme.xaml and recolours WinUI's built-in controls
    MainWindow.xaml      custom title bar, sidebar, page frame, update dialog, toast
    Pages/               one page per screen
    Pages/Settings/      settings host + Account, General, Reader, Library, Trackers, System
    Controls/            MdIcon (Lucide-style icons), MdProgress, SettingRow, SearchField, PagePlaceholder
    Helpers/X.cs         small functions used by x:Bind (colours, visibility, status text)
    Services/Nav.cs      navigation, sidebar mapping, toast, update dialog
    Services/ThemeService.cs   accent colour switching (Settings › General)
    Native/NativeMethods.cs    P/Invoke stubs for the C++ DLL
  MangaDl.Native/        C++20 DLL placeholder (image work, archives, cropping)
```

## Wiring the backend

- Replace `Sample.*` in `MangaDl.Core` with real services. ViewModels can use
  CommunityToolkit.Mvvm, which is already referenced. Pages bind with `x:Bind`, so
  expose `ObservableCollection`s / `ObservableObject`s with the same property names.
- Search for `TODO(backend)` to find every stub action.
- Persist the accent colour from `ThemeService.SetAccent`.
- Navigation: `Nav.Go(typeof(Page), parameter)`. Parameters that are used:
  - `MangaDetailPage` and `ReaderPage` take a `Manga`; `ReaderPage` also takes `"single"`.
  - `ExtensionsPage` takes `"sources"` or `"migrate"`.
  - `BrowseSourcePage` takes the source name.
  - `SettingsPage` takes a section key: `Account`, `General`, `Reader`, `Library`, `Trackers` or `System`.

### Shipping the C++ DLL

`MangaDl.Native` builds on its own. To bundle it, add this to `MangaDl.App.csproj` once you
start calling it:

```xml
<ItemGroup>
  <Content Include="..\MangaDl.Native\$(Platform)\$(Configuration)\MangaDl.Native.dll"
           Condition="'$(Platform)'!='x86'" CopyToOutputDirectory="PreserveNewest" Link="MangaDl.Native.dll" />
</ItemGroup>
```

(For x86, the C++ output folder is `Win32`.) Also add a project dependency in the solution
so it builds first.

## Fonts and assets

`Assets/Fonts` holds Anton, Inter (variable) and PT Serif. All three use the SIL Open Font
License. Logos in `Assets/` are generated placeholders; replace them with final art at the
same sizes.

## Status: please read

This was written on Linux, where WinUI 3 cannot be compiled.
- `MangaDl.Core` builds.
- Every XAML file has been checked for well-formed XML.
- **The app itself has not been compiled or run yet.** Expect a few compile errors on the
  first build. Paste them back and they'll be fixed.

These are the places most likely to need a tweak:

| Where | What |
|---|---|
| `Controls/MdIcon.cs` | Path strings are parsed with `XamlBindingHelper.ConvertValue(typeof(Geometry), …)` |
| `Themes/Theme.xaml` | `FontFamily` is `ms-appx:///Assets/Fonts/InterVariable.ttf#Inter Variable`; if Inter falls back to Segoe, try `#Inter` |
| `Themes/Theme.xaml` | `MdTextBox` and `MdPasswordBox` are `BasedOn` WinUI's `DefaultTextBoxStyle` / `DefaultPasswordBoxStyle` |
| Extensions and Trackers pages | `Style="{x:Bind h:X.…Style(…)}"` (Style returned from a function) |
| `MainWindow.xaml.cs` | `OverlappedPresenter.PreferredMinimumWidth/Height` (Windows App SDK 1.7+) |

Not included, as agreed: DownloadHub, Landing and Terms.
