using HNReader.Core.Viewmodels;
using Microsoft.UI.Xaml.Controls;

namespace HNReader.WinUI.Views;

public sealed partial class TopPage : Page
{
    public TopPage(TopPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
