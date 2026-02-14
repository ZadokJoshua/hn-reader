using HNReader.Core.Enums;
using HNReader.Core.Viewmodels;
using HNReader.WinUI.Factories;
using Microsoft.UI.Xaml.Controls;
using System;

namespace HNReader.WinUI.Services;

public class NavigationService(PageFactory pageFactory, IServiceProvider serviceProvider)
{
    private Frame? _frame;

    public event Action<ApplicationPages>? Navigated;

    public void NavigateToPage(ApplicationPages page)
    {
        if (_frame == null) return;

        var pageInstance = pageFactory.GetPage(page);
        _frame.Content = pageInstance;

        Navigated?.Invoke(page);
        
        //// Settings page doesn't have a PageViewModel, just set DataContext directly
        //if (page == ApplicationPages.Settings) return;

        var viewModel = pageFactory.GetPageViewModel(page);
        pageInstance.DataContext = viewModel;

        // Trigger data loading directly on the view model
        if (viewModel is PageViewModel pageViewModel) _ = pageViewModel.PopulateListAsync();
    }

    public void Initialize(Frame shellFrame) => _frame = shellFrame;
}
