using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using HNReader.Avalonia.Services;
using Microsoft.Extensions.DependencyInjection;
using MdStyle = Markdown.Avalonia.MarkdownStyle;

namespace HNReader.Avalonia.Controls;

/// <summary>
/// Renders markdown as plain content, without a scroll viewer wrapped around it.
/// </summary>
/// <remarks>
/// MarkdownScrollViewer is a Control that builds its own ScrollViewer, Grid, content
/// presenter, internal Wrapper, Canvas and two ScrollBars - roughly nine controls per
/// instance whose only purpose is scrolling. A comment never scrolls on its own; the
/// comments pane scrolls as a whole. Across a long thread that is thousands of controls
/// of pure overhead, re-measured on every window resize.
///
/// This hosts the engine's output directly instead. Formatting, links, code blocks and
/// quotes are unchanged because it is the same engine doing the same work - the setup
/// below mirrors what MarkdownScrollViewer's constructor does.
/// </remarks>
public class MarkdownBlock : ContentControl
{
    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownBlock, string?>(nameof(Markdown));

    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(MarkdownBlock);

    private readonly Markdown.Avalonia.Markdown _engine;

    static MarkdownBlock()
    {
        MarkdownProperty.Changed.AddClassHandler<MarkdownBlock>((block, args) =>
            block.Render(args.NewValue as string));
    }

    public MarkdownBlock()
    {
        _engine = new Markdown.Avalonia.Markdown
        {
            // Without this a link renders but does nothing when clicked -
            // MarkdownScrollViewer was supplying it on our behalf.
            HyperlinkCommand = OpenLinkCommand.Instance,
        };

        // Lets the generated document resolve DynamicResource lookups through this
        // control, which is how the markdown picks up the app's theme brushes.
        _engine.CascadeResources.SetParent(this);

        // No per-instance styles on purpose. The markdown theme and the link/list overrides
        // are registered once on Application.Styles (see App.OnFrameworkInitializationCompleted
        // and Styles/MarkdownComments.axaml). Adding them here gave every comment its own
        // private style stack to resolve, which is a real cost across a long thread.
    }

    // Set when markdown arrives while the block is hidden (a collapsed comment's body), so the
    // expensive Transform is paid when it is first shown rather than for every hidden comment.
    private string? _pendingMarkdown;
    private bool _hasPending;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsVisibleProperty && change.NewValue is true && _hasPending)
        {
            var pending = _pendingMarkdown;
            _pendingMarkdown = null;
            _hasPending = false;
            RenderNow(pending);
        }
    }

    private void Render(string? markdown)
    {
        if (!IsVisible)
        {
            _pendingMarkdown = markdown;
            _hasPending = true;
            return;
        }

        _hasPending = false;
        _pendingMarkdown = null;
        RenderNow(markdown);
    }

    private void RenderNow(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            Content = null;
            return;
        }

        try
        {
            var rendered = _engine.Transform(markdown);

            // MarkdownScrollViewer adds this class to the document root, and every list,
            // marker, paragraph and code-block style in Markdown.Avalonia's theme is scoped
            // beneath it. Hosting the output directly skipped it, which is why bullets sat
            // flush against their text with no indent and paragraphs had no spacing.
            rendered.Classes.Add("Markdown_Avalonia_MarkdownViewer");
            Content = rendered;
        }
        catch
        {
            // A single malformed comment must not take down the thread. Fall back to
            // raw text, matching how WebCommentNode handles a failed conversion.
            Content = new SelectableTextBlock
            {
                Text = markdown,
                TextWrapping = TextWrapping.Wrap,
            };
        }
    }

    /// <summary>
    /// Opens a markdown link in the default browser.
    /// </summary>
    private sealed class OpenLinkCommand : ICommand
    {
        public static readonly OpenLinkCommand Instance = new();

        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public async void Execute(object? parameter)
        {
            var url = parameter?.ToString();

            // Comment links go through the confirmation prompt (if enabled) so the user
            // sees the real destination first. Falls back to a direct open when the
            // service is unavailable, e.g. in the XAML previewer.
            var service = (global::Avalonia.Application.Current as App)?.Services.GetService<LinkConfirmationService>();
            try
            {
                if (service is not null)
                {
                    await service.OpenAsync(url);
                }
                else
                {
                    UrlOpener.TryOpen(url);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Opening comment link failed: {ex.Message}");
            }
        }
    }
}
