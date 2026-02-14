using HNReader.Core.Viewmodels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace HNReader.WinUI.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = ViewModel;
    }

    private async void SelectVaultButton_Click(object sender, RoutedEventArgs e)
    {
        var folderPicker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        folderPicker.FileTypeFilter.Add("*");

        // Get the current window handle for the picker (UI-specific)
        var window = App.CurrentWindow;
        if (window != null)
        {
            var hwnd = WindowNative.GetWindowHandle(window);
            InitializeWithWindow.Initialize(folderPicker, hwnd);
        }

        var folder = await folderPicker.PickSingleFolderAsync();
        if (folder != null) ViewModel.SetVaultPath(folder.Path);
    }
}
