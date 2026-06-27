using HNReader.Core.Viewmodels;
using Microsoft.UI.Xaml.Controls;

namespace HNReader.WinUI.Views;

public sealed partial class AskPage : Page
{
    public AskPage(AskPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
