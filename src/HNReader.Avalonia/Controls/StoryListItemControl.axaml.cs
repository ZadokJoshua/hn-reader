using Avalonia;
using Avalonia.Controls;

namespace HNReader.Avalonia.Controls;

public partial class StoryListItemControl : UserControl
{
    public StoryListItemControl()
    {
        InitializeComponent();

        // RootGrid starts at Opacity="0" in XAML; flipping it to 1 here (once
        // attached) lets its Transitions block fade the row in as it's
        // realized, instead of popping straight in during list virtualization.
        AttachedToVisualTree += (_, _) => RootGrid.Opacity = 1;
    }

    public static readonly StyledProperty<int> ScoreProperty =
        AvaloniaProperty.Register<StoryListItemControl, int>(nameof(Score));

    public int Score
    {
        get => GetValue(ScoreProperty);
        set => SetValue(ScoreProperty, value);
    }

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<StoryListItemControl, string>(nameof(Title), string.Empty);

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly StyledProperty<string> UrlProperty =
        AvaloniaProperty.Register<StoryListItemControl, string>(nameof(Url), string.Empty);

    public string Url
    {
        get => GetValue(UrlProperty);
        set => SetValue(UrlProperty, value);
    }

    public static readonly StyledProperty<int> DescendantsProperty =
        AvaloniaProperty.Register<StoryListItemControl, int>(nameof(Descendants));

    public int Descendants
    {
        get => GetValue(DescendantsProperty);
        set => SetValue(DescendantsProperty, value);
    }

    public static readonly StyledProperty<string> TimeAgoProperty =
        AvaloniaProperty.Register<StoryListItemControl, string>(nameof(TimeAgo), string.Empty);

    public string TimeAgo
    {
        get => GetValue(TimeAgoProperty);
        set => SetValue(TimeAgoProperty, value);
    }

    public static readonly StyledProperty<string> ByProperty =
        AvaloniaProperty.Register<StoryListItemControl, string>(nameof(By), string.Empty);

    public string By
    {
        get => GetValue(ByProperty);
        set => SetValue(ByProperty, value);
    }

    public static readonly StyledProperty<bool> IsFavoriteProperty =
        AvaloniaProperty.Register<StoryListItemControl, bool>(nameof(IsFavorite));

    public bool IsFavorite
    {
        get => GetValue(IsFavoriteProperty);
        set => SetValue(IsFavoriteProperty, value);
    }

    /// <summary>The domain shown under the title.</summary>
    public static readonly StyledProperty<string> DisplayDomainProperty =
        AvaloniaProperty.Register<StoryListItemControl, string>(nameof(DisplayDomain), string.Empty);

    public string DisplayDomain
    {
        get => GetValue(DisplayDomainProperty);
        set => SetValue(DisplayDomainProperty, value);
    }

    /// <summary>
    /// The host used to fetch the icon. Separate from <see cref="DisplayDomain"/> because
    /// what is shown and what is fetched are different contracts.
    /// </summary>
    public static readonly StyledProperty<string> FaviconHostProperty =
        AvaloniaProperty.Register<StoryListItemControl, string>(nameof(FaviconHost), string.Empty);

    public string FaviconHost
    {
        get => GetValue(FaviconHostProperty);
        set => SetValue(FaviconHostProperty, value);
    }
}
