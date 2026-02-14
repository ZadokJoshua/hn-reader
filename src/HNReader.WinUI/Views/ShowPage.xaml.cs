using HNReader.Core.Viewmodels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace HNReader.WinUI.Views;

public sealed partial class ShowPage : Page
{
    private readonly ShowPageViewModel _viewModel;

    public ShowPage(ShowPageViewModel viewModel)
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
