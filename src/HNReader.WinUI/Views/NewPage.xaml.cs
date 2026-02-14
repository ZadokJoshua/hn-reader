using HNReader.Core.Viewmodels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace HNReader.WinUI.Views;

public sealed partial class NewPage : Page
{
    private readonly NewPageViewModel _viewModel;

    public NewPage(NewPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await _viewModel.PopulateListAsync();
    }
}
