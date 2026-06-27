using HNReader.Core.Viewmodels;
using Microsoft.UI.Xaml.Controls;

namespace HNReader.WinUI.Views;

public sealed partial class NewPage : Page
{
    public NewPage(NewPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
