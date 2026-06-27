using HNReader.Core.Viewmodels;
using Microsoft.UI.Xaml.Controls;

namespace HNReader.WinUI.Views;

public sealed partial class BestPage : Page
{
    public BestPage(BestPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
