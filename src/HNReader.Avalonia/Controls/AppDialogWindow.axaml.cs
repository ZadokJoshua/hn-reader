using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace HNReader.Avalonia.Controls;

/// <summary>
/// Which button closed an <see cref="AppDialogWindow"/>.
/// Replaces FluentAvalonia's ContentDialogResult.
/// </summary>
public enum DialogResult
{
    None,
    Primary,
    Secondary
}

/// <summary>
/// Modal dialog with up to three buttons. Replaces FluentAvalonia's ContentDialog.
/// </summary>
/// <remarks>
/// A real top-level window rather than an in-window overlay, which is why it needs
/// a live owner. That matches the previous behaviour: the only caller already
/// returned early when there was no main window to attach to.
/// </remarks>
public partial class AppDialogWindow : Window
{
    public AppDialogWindow()
    {
        InitializeComponent();

        PART_Primary.Click += (_, _) => Close(DialogResult.Primary);
        PART_Secondary.Click += (_, _) => Close(DialogResult.Secondary);
        PART_Close.Click += (_, _) => Close(DialogResult.None);

        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        Close(DialogResult.None);
        e.Handled = true;
    }

    /// <summary>
    /// Shows a modal dialog over <paramref name="owner"/> and returns which button
    /// dismissed it. A null button caption hides that button.
    /// </summary>
    public static Task<DialogResult> ShowAsync(
        Window owner,
        string title,
        object? content,
        string? primaryText = null,
        string? secondaryText = null,
        string closeText = "OK",
        bool showHeading = true)
    {
        var dialog = new AppDialogWindow { Title = title };

        dialog.PART_Title.Text = title;
        // The OS title bar still carries the title; callers whose body is self-explanatory can drop the repeat.
        dialog.PART_Title.IsVisible = showHeading;
        dialog.PART_Content.Content = content;

        dialog.PART_Primary.Content = primaryText;
        dialog.PART_Primary.IsVisible = primaryText is not null;

        dialog.PART_Secondary.Content = secondaryText;
        dialog.PART_Secondary.IsVisible = secondaryText is not null;

        dialog.PART_Close.Content = closeText;

        return dialog.ShowDialog<DialogResult>(owner);
    }
}
