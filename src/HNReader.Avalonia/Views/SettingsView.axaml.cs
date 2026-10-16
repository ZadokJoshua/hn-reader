using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HNReader.Avalonia.Controls;
using HNReader.Core.Helpers;
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
        const string failureTitle = "Couldn't export your favourites";

        try
        {
            var storage = GetStorageProvider();
            if (storage is null) return;

            // Checked before the save dialog so the user is not asked to pick a
            // location for a file that would only contain an empty list.
            var existing = await _favoritesService.Value.GetAllIdsAsync();
            if (existing.Count == 0)
            {
                ShowStatus("Nothing to export yet", "Save a story as a favourite first, then come back to back it up.",
                    StatusSeverity.Informational);
                return;
            }

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
            ShowStatus("Favourites exported",
                $"Saved {count} favourite{(count == 1 ? "" : "s")} to {file.Name}.",
                StatusSeverity.Success);
        }
        catch (Exception ex)
        {
            ReportFailure("exporting favourites", failureTitle, ex);
        }
    }

    private async void ImportFavoritesButton_Click(object? sender, RoutedEventArgs e)
    {
        const string failureTitle = "Couldn't import favourites";

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

            var result = await _favoritesService.Value.ImportAsync(file.Path.LocalPath);

            var message = $"Imported {result.Total} favourite{(result.Total == 1 ? "" : "s")} from {file.Name}.";
            if (result.Updated > 0 && result.Added == 0)
            {
                message = $"All {result.Total} favourite{(result.Total == 1 ? " was" : "s were")} already saved. Nothing new to add.";
            }
            else if (result.Updated > 0)
            {
                message = $"Added {result.Added} new favourite{(result.Added == 1 ? "" : "s")} ({result.Updated} already saved).";
            }

            if (result.Skipped > 0)
            {
                message += $" {result.Skipped} entr{(result.Skipped == 1 ? "y" : "ies")} in the file couldn't be used.";
            }

            ShowStatus("Import complete", message,
                result.Added == 0 ? StatusSeverity.Informational : StatusSeverity.Success);
        }
        catch (Exception ex)
        {
            ReportFailure("importing favourites", failureTitle, ex);
        }
    }

    /// <summary>
    /// The user sees one plain sentence; the exception, with its technical detail, goes to the log.
    /// </summary>
    private void ReportFailure(string operation, string title, Exception ex)
    {
        (global::Avalonia.Application.Current as App)?.Services.GetService<ILogger>()
            ?.LogError("SettingsView", $"Error {operation}", ex);

        ShowStatus(title, ErrorMessageHelper.Describe(ex).Message, StatusSeverity.Error);
    }

    private void ShowStatus(string title, string message, StatusSeverity severity)
    {
        StatusBannerControl.Severity = severity;
        StatusBannerControl.Title = title;
        StatusBannerControl.Message = message;
        StatusBannerControl.IsOpen = true;
    }
}
