using System.Collections.ObjectModel;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace MangaDl.Pages.Settings;

public sealed record CategoryRow(string Name, string Count);

public sealed partial class LibrarySettingsPage : Page
{
    public ObservableCollection<CategoryRow> Categories { get; } =
    [
        new("Default", "0 manga"),
        new("Reading", "0 manga"),
        new("Plan to read", "0 manga"),
        new("Completed", "0 manga"),
        new("Local files", "0 manga"),
    ];

    private string? _renaming;

    public LibrarySettingsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadCategoriesAsync();
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var list = await AppServices.Database.GetCategoriesAsync();
            if (list.Count > 0)
            {
                Categories.Clear();
                foreach (var c in list)
                {
                    Categories.Add(new CategoryRow(c.Name, "0 manga"));
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("LibrarySettingsPage.LoadCategoriesAsync", ex);
            Nav.Toast("Couldn't load categories");
        }
    }

    private void OnAdd(object sender, RoutedEventArgs e) => OpenEditor(null);

    private void OnRename(object sender, RoutedEventArgs e) => OpenEditor((string)((FrameworkElement)sender).Tag);

    private void OpenEditor(string? name)
    {
        _renaming = name;
        NewName.Text = name ?? "";
        AddRow.Visibility = Visibility.Visible;
        NewName.Focus(FocusState.Programmatic);
    }

    private async void OnDelete(object sender, RoutedEventArgs e)
    {
        var name = (string)((FrameworkElement)sender).Tag;
        var row = Categories.FirstOrDefault(c => c.Name == name);
        if (row is not null)
        {
            Categories.Remove(row);
            try
            {
                await AppServices.Database.DeleteCategoryAsync(name.ToLowerInvariant().Replace(" ", "_"));
                Nav.Toast($"Deleted \"{name}\"");
            }
            catch (Exception ex)
            {
                AppLog.Warn("LibrarySettingsPage.OnDelete", ex);
                Categories.Add(row);
                Nav.Toast($"Couldn't delete \"{name}\"");
            }
        }
    }

    private void OnSaveNew(object sender, RoutedEventArgs e) => Save();

    private void OnNewNameKey(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter) Save();
        else if (e.Key == VirtualKey.Escape) Close();
    }

    private void OnCancelNew(object sender, RoutedEventArgs e) => Close();

    private async void Save()
    {
        var name = NewName.Text.Trim();
        if (name.Length == 0) return;
        var existing = Categories.FirstOrDefault(c => c.Name == _renaming);
        if (existing is not null)
        {
            Categories[Categories.IndexOf(existing)] = existing with { Name = name };
        }
        else
        {
            var row = new CategoryRow(name, "0 manga");
            Categories.Add(row);
            try
            {
                await AppServices.Database.AddCategoryAsync(name.ToLowerInvariant().Replace(" ", "_"), name);
            }
            catch (Exception ex)
            {
                AppLog.Warn("LibrarySettingsPage.Save", ex);
                Categories.Remove(row);
                Nav.Toast($"Couldn't add \"{name}\"");
            }
        }
        Close();
    }

    private void Close()
    {
        _renaming = null;
        NewName.Text = "";
        AddRow.Visibility = Visibility.Collapsed;
    }

    private void OnMigrate(object sender, RoutedEventArgs e) => Nav.Go(typeof(ExtensionsPage), "migrate");
}
