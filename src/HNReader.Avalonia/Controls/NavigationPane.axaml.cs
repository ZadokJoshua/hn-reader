using Avalonia.Controls;

namespace HNReader.Avalonia.Controls;

/// <summary>
/// The app's side navigation pane. Its DataContext is a NavigationViewModel,
/// assigned by the shell.
/// </summary>
public partial class NavigationPane : UserControl
{
    public NavigationPane()
    {
        InitializeComponent();
    }
}
