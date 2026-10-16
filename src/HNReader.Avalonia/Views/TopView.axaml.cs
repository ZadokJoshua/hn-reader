using Avalonia.Controls;
using HNReader.Core.Viewmodels;

namespace HNReader.Avalonia.Views;

public partial class TopView : UserControl
{
    public TopView() : this(null!) { }

    public TopView(TopPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
