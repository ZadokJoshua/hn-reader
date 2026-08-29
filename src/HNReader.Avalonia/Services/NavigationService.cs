using System;
using Avalonia.Controls;
using HNReader.Avalonia.Factories;
using HNReader.Core.Enums;
using HNReader.Core.Viewmodels;

namespace HNReader.Avalonia.Services;

/// <summary>
/// Avalonia has no Frame/Page navigation stack, so this manually swaps the
/// Content of a ContentControl instead — mirroring the WinUI NavigationService's
/// Frame.Content assignment pattern.
/// </summary>
public class NavigationService(PageFactory pageFactory)
{
    private ContentControl? _host;

    public event Action<ApplicationPages>? Navigated;

    public void Initialize(ContentControl host) => _host = host;

    public void NavigateToPage(ApplicationPages page)
    {
        if (_host == null) return;

        if (_host.Content is UserControl previousPage)
        {
            if (previousPage.DataContext is IDisposable disposableViewModel)
            {
                disposableViewModel.Dispose();
            }

            previousPage.DataContext = null;
        }

        var pageInstance = pageFactory.GetPage(page);
        _host.Content = pageInstance;

        Navigated?.Invoke(page);

        var viewModel = pageFactory.GetPageViewModel(page);
        pageInstance.DataContext = viewModel;

        if (viewModel is PageViewModel pageViewModel) _ = pageViewModel.PopulateListAsync();
    }
}
