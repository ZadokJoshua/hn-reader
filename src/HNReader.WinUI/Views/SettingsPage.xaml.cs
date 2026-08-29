using HNReader.Core.Constants;
using HNReader.Core.Interfaces;
using HNReader.Core.Services.Logging;
using HNReader.Core.Viewmodels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
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

    private async void ExportLogsButton_Click(object sender, RoutedEventArgs e)
    {
        var logger = (Application.Current as App)?.Services.GetService<ILogger>();
        if (logger is null)
        {
            ShowDiagnosticsStatus("Logger is not available.", InfoBarSeverity.Error);
            return;
        }

        string? destPath = null;
        try
        {
            var downloads = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads");
            Directory.CreateDirectory(downloads);

            var fileName = $"hnreader-logs-{DateTime.Now:yyyyMMdd-HHmmss}.zip";
            destPath = Path.Combine(downloads, fileName);

            // Build the zip from a temp path first, then rotate the logger to release
            // the active file's OS handle, then move the zip into place. This avoids
            // sharing-violation errors when the active log file is still open.
            var tempZip = Path.Combine(
                Path.GetTempPath(),
                $"hnreader-logs-{Guid.NewGuid():N}.zip");

            await logger.FlushAsync();
            await Task.Run(() => ZipFile.CreateFromDirectory(logger.LogDirectory, tempZip));
            await logger.RotateAsync();
            File.Move(tempZip, destPath);

            ShowDiagnosticsStatus($"Exported logs to {fileName}.", InfoBarSeverity.Success);

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{destPath}\"",
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings] Failed to open Explorer for {destPath}: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            ShowDiagnosticsStatus($"Export failed: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void ReportBugButton_Click(object sender, RoutedEventArgs e)
    {
        var url = AppFileNames.BUG_REPORT_URL;
        if (string.IsNullOrWhiteSpace(url)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            ShowDiagnosticsStatus($"Could not open the bug-report form: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void ShowDiagnosticsStatus(string message, InfoBarSeverity severity)
    {
        DiagnosticsInfoBar.Severity = severity;
        DiagnosticsInfoBar.Title = severity == InfoBarSeverity.Success ? "Success" : "Error";
        DiagnosticsInfoBar.Message = message;
        DiagnosticsInfoBar.IsOpen = true;
    }
}
