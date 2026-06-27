using HNReader.Core.Interfaces;
using HNReader.Core.Viewmodels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace HNReader.WinUI.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }
    private readonly Lazy<IFavoritesService> _favoritesService;

    public SettingsPage(SettingsViewModel viewModel, Lazy<IFavoritesService> favoritesService)
    {
        ViewModel = viewModel;
        _favoritesService = favoritesService;
        InitializeComponent();
        DataContext = ViewModel;
    }

    private async void ExportFavoritesButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = $"hn-favorites-{DateTime.Now:yyyyMMdd-HHmmss}"
            };
            picker.FileTypeChoices.Add("JSON file", new[] { ".json" });

            var window = App.CurrentWindow;
            if (window != null)
            {
                var hwnd = WindowNative.GetWindowHandle(window);
                InitializeWithWindow.Initialize(picker, hwnd);
            }

            var file = await picker.PickSaveFileAsync();
            if (file == null) return;

            var count = await _favoritesService.Value.ExportToFileAsync(file.Path);
            ShowStatus($"Exported {count} favourite{(count == 1 ? "" : "s")} to {file.Name}.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"Export failed: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void ImportFavoritesButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                ViewMode = PickerViewMode.List
            };
            picker.FileTypeFilter.Add(".json");

            var window = App.CurrentWindow;
            if (window != null)
            {
                var hwnd = WindowNative.GetWindowHandle(window);
                InitializeWithWindow.Initialize(picker, hwnd);
            }

            var file = await picker.PickSingleFileAsync();
            if (file == null) return;

            var count = await _favoritesService.Value.ImportFromFileAsync(file.Path);
            ShowStatus($"Imported {count} favourite{(count == 1 ? "" : "s")} from {file.Name}.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"Import failed: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusInfoBar.Severity = severity;
        StatusInfoBar.Title = severity == InfoBarSeverity.Success ? "Success" : "Error";
        StatusInfoBar.Message = message;
        StatusInfoBar.IsOpen = true;
    }
}
