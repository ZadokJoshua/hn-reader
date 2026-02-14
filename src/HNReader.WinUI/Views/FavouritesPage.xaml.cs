using HNReader.Core.Viewmodels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace HNReader.WinUI.Views;

public sealed partial class FavouritesPage : Page
{
    private readonly FavouritesPageViewModel _viewModel;

    public FavouritesPage(FavouritesPageViewModel viewModel)
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
