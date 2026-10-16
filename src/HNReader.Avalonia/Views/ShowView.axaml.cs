using Avalonia.Controls;
using HNReader.Core.Viewmodels;

namespace HNReader.Avalonia.Views;

public partial class ShowView : UserControl
{
    public ShowView() : this(null!) { }

    public ShowView(ShowPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
