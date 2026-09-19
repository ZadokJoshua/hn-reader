using Avalonia.Controls;
using HNReader.Core.Viewmodels;

namespace HNReader.Avalonia.Views;

public partial class DigestView : UserControl
{
    // Parameterless constructor kept for the Avalonia XAML previewer only.
    public DigestView() : this(null!) { }

    public DigestView(DigestPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
