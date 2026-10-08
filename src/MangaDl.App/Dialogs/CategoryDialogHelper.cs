using MangaDl.Core.Database.Entities;
using MangaDl.Core.Local;
using MangaDl.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MangaDl.Dialogs;

public static class CategoryDialogHelper
{
    public static async Task<bool> ShowAsync(XamlRoot xamlRoot, string provider, string mangaId, string mangaTitle)
    {
        var allCategories = await AppServices.Database.GetCategoriesAsync();
        var currentCategoryIds = (await AppServices.Database.GetMangaCategoriesAsync(provider, mangaId)).ToHashSet();

        var panel = new StackPanel
        {
            Spacing = 12,
            Width = 360
        };

        var subtitle = new TextBlock
        {
            Text = $"Select shelves for \"{mangaTitle}\"",
            Style = Application.Current.Resources.TryGetValue("Caption", out var capStyle) ? (Style)capStyle : null,
            Margin = new Thickness(0, 0, 0, 8)
        };
        panel.Children.Add(subtitle);

        var listPanel = new StackPanel { Spacing = 4 };
        var checkBoxes = new List<CheckBox>();

        void AddCategoryCheckBox(CategoryEntity cat, bool isChecked)
        {
            var cb = new CheckBox
            {
                Content = cat.Name,
                Tag = cat.Id,
                IsChecked = isChecked,
                Margin = new Thickness(0, 2, 0, 2)
            };
            checkBoxes.Add(cb);
            listPanel.Children.Add(cb);
        }

        foreach (var cat in allCategories)
        {
            AddCategoryCheckBox(cat, currentCategoryIds.Contains(cat.Id));
        }

        var scroll = new ScrollViewer
        {
            Content = listPanel,
            MaxHeight = 240
        };
        panel.Children.Add(scroll);

        // Add new category row
        var addGrid = new Grid
        {
            Margin = new Thickness(0, 12, 0, 0),
            ColumnSpacing = 8
        };
        addGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        addGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var newCatBox = new TextBox
        {
            PlaceholderText = "New category name...",
            VerticalAlignment = VerticalAlignment.Center
        };
        var addBtn = new Button
        {
            Content = "Add",
            Style = Application.Current.Resources.TryGetValue("GhostButton", out var btnStyle) ? (Style)btnStyle : null,
            VerticalAlignment = VerticalAlignment.Center
        };

        addBtn.Click += async (_, _) =>
        {
            var name = newCatBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name)) return;

            var id = FileNaming.Slugify(name);
            if (checkBoxes.Any(c => (string)c.Tag == id))
            {
                newCatBox.Text = string.Empty;
                return;
            }

            try
            {
                await AppServices.Database.AddCategoryAsync(id, name, checkBoxes.Count);
                AddCategoryCheckBox(new CategoryEntity { Id = id, Name = name, SortOrder = checkBoxes.Count }, isChecked: true);
                newCatBox.Text = string.Empty;
            }
            catch (Exception ex)
            {
                AppLog.Warn("CategoryDialogHelper.AddCategory", ex);
            }
        };

        Grid.SetColumn(newCatBox, 0);
        Grid.SetColumn(addBtn, 1);
        addGrid.Children.Add(newCatBox);
        addGrid.Children.Add(addBtn);
        panel.Children.Add(addGrid);

        var dialog = new ContentDialog
        {
            Title = "Manage Categories",
            Content = panel,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var selectedIds = checkBoxes
                .Where(c => c.IsChecked == true)
                .Select(c => (string)c.Tag)
                .ToList();

            await AppServices.Database.SetMangaCategoriesAsync(provider, mangaId, selectedIds);
            return true;
        }

        return false;
    }
}
