using HNReader.Core.Viewmodels;
using Microsoft.UI.Xaml.Controls;

namespace HNReader.WinUI.Views;

public sealed partial class FavouritesPage : Page
{
    public FavouritesPage(FavouritesPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
