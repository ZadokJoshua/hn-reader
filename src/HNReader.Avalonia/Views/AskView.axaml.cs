using Avalonia.Controls;
using HNReader.Core.Viewmodels;

namespace HNReader.Avalonia.Views;

public partial class AskView : UserControl
{
    public AskView() : this(null!) { }

    public AskView(AskPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
