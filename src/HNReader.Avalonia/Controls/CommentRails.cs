using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace HNReader.Avalonia.Controls;

/// <summary>
/// Draws one vertical thread rail per ancestor level of a comment.
/// </summary>
/// <remarks>
/// Replaces an ItemsControl that built a Panel and a Border per level. That cost
/// roughly 2*Depth+2 controls on every comment in the thread, which more than ate the
/// saving from dropping MarkdownScrollViewer - a 243-comment thread measured 6,261
/// controls with it. Rails carry no content and take no input, so there is nothing a
/// control tree buys here that a single Render pass does not.
///
/// Rows sit flush against each other, so each row's segments line up with its
/// neighbours' into the continuous rails the old nested layout produced.
/// </remarks>
public class CommentRails : Control
{
    /// <summary>Horizontal space each nesting level occupies.</summary>
    private const double LevelWidth = 20;

    private const double RailThickness = 2;

    public static readonly StyledProperty<int> DepthProperty =
        AvaloniaProperty.Register<CommentRails, int>(nameof(Depth));

    public static readonly StyledProperty<IBrush?> RailBrushProperty =
        AvaloniaProperty.Register<CommentRails, IBrush?>(nameof(RailBrush));

    public int Depth
    {
        get => GetValue(DepthProperty);
        set => SetValue(DepthProperty, value);
    }

    public IBrush? RailBrush
    {
        get => GetValue(RailBrushProperty);
        set => SetValue(RailBrushProperty, value);
    }

    static CommentRails()
    {
        // Depth changes the width, so it has to invalidate measure as well as paint.
        AffectsMeasure<CommentRails>(DepthProperty);
        AffectsRender<CommentRails>(DepthProperty, RailBrushProperty);
    }

    protected override Size MeasureOverride(Size availableSize)
        => new(Depth * LevelWidth, 0);

    public override void Render(DrawingContext context)
    {
        if (Depth <= 0 || RailBrush is null) return;

        var pen = new Pen(RailBrush, RailThickness);
        var height = Bounds.Height;

        for (var level = 0; level < Depth; level++)
        {
            var x = (level * LevelWidth) + (LevelWidth / 2);
            context.DrawLine(pen, new Point(x, 0), new Point(x, height));
        }
    }
}
