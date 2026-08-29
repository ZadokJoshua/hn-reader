using Avalonia.Controls;
using HNReader.Core.Viewmodels;

namespace HNReader.Avalonia.Views;

public partial class FavouritesView : UserControl
{
    public FavouritesView() : this(null!) { }

    public FavouritesView(FavouritesPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
