using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FluentAvalonia.UI.Controls;
using HNReader.Avalonia.Services;
using HNReader.Core.Constants;
using HNReader.Core.Interfaces;
using HNReader.Core.Services.Logging;
using HNReader.Core.Viewmodels;
using Microsoft.Extensions.DependencyInjection;

namespace HNReader.Avalonia.Views;

public partial class SettingsView : UserControl
{
    public SettingsViewModel ViewModel { get; }
    private readonly Lazy<IFavoritesService> _favoritesService;

    public SettingsView() : this(null!, null!) { }

    public SettingsView(SettingsViewModel viewModel, Lazy<IFavoritesService> favoritesService)
    {
        ViewModel = viewModel;
        _favoritesService = favoritesService;
        InitializeComponent();
        DataContext = ViewModel;
    }

    private IStorageProvider? GetStorageProvider() => TopLevel.GetTopLevel(this)?.StorageProvider;

    private async void ExportFavoritesButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var storage = GetStorageProvider();
            if (storage is null) return;

            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = $"hn-favorites-{DateTime.Now:yyyyMMdd-HHmmss}",
                DefaultExtension = "json",
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new("JSON file") { Patterns = new[] { "*.json" } }
                }
            });
            if (file is null) return;

            var count = await _favoritesService.Value.ExportToFileAsync(file.Path.LocalPath);
            ShowStatus($"Exported {count} favourite{(count == 1 ? "" : "s")} to {file.Name}.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"Export failed: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void ImportFavoritesButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var storage = GetStorageProvider();
            if (storage is null) return;

            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new("JSON file") { Patterns = new[] { "*.json" } }
                }
            });
            var file = files.FirstOrDefault();
            if (file is null) return;

            var count = await _favoritesService.Value.ImportFromFileAsync(file.Path.LocalPath);
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

    private async void ExportLogsButton_Click(object? sender, RoutedEventArgs e)
    {
        var logger = (global::Avalonia.Application.Current as App)?.Services.GetService<ILogger>();
        if (logger is null)
        {
            ShowDiagnosticsStatus("Logger is not available.", InfoBarSeverity.Error);
            return;
        }

        try
        {
            var storage = GetStorageProvider();
            if (storage is null) return;

            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = $"hnreader-logs-{DateTime.Now:yyyyMMdd-HHmmss}",
                DefaultExtension = "zip",
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new("Zip archive") { Patterns = new[] { "*.zip" } }
                }
            });
            if (file is null) return;

            var tempZip = Path.Combine(Path.GetTempPath(), $"hnreader-logs-{Guid.NewGuid():N}.zip");

            // Rotate BEFORE zipping: rotation is what actually closes today's log
            // file handle (via LogFileWriter.Rotate -> CloseCore/OpenNew). Zipping
            // first would try to read a file still open for writing and fail with
            // an IOException.
            await logger.FlushAsync();
            await logger.RotateAsync();
            await Task.Run(() => ZipFile.CreateFromDirectory(logger.LogDirectory, tempZip));

            using (var src = File.OpenRead(tempZip))
            await using (var dst = await file.OpenWriteAsync())
            {
                await src.CopyToAsync(dst);
            }
            File.Delete(tempZip);

            ShowDiagnosticsStatus($"Exported logs to {file.Name}.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowDiagnosticsStatus($"Export failed: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void ReportBugButton_Click(object? sender, RoutedEventArgs e)
    {
        var url = AppFileNames.BUG_REPORT_URL;
        if (string.IsNullOrWhiteSpace(url)) return;

        if (!UrlOpener.TryOpen(url))
        {
            ShowDiagnosticsStatus("Could not open the bug-report form.", InfoBarSeverity.Error);
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
