using Avalonia.Controls;
using HNReader.Core.Viewmodels;

namespace HNReader.Avalonia.Views;

public partial class BestView : UserControl
{
    public BestView() : this(null!) { }

    public BestView(BestPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
