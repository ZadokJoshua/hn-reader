using Avalonia.Controls;
using HNReader.Core.Viewmodels;

namespace HNReader.Avalonia.Views;

public partial class NewView : UserControl
{
    public NewView() : this(null!) { }

    public NewView(NewPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
