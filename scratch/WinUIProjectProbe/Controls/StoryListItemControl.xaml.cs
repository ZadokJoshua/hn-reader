using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HNReader.WinUI.Controls;

public sealed partial class StoryListItemControl : UserControl
{
    public StoryListItemControl()
    {
        InitializeComponent();
    }

    // Bindable Properties
    public int Score
    {
        get { return (int)GetValue(ScoreProperty); }
        set { SetValue(ScoreProperty, value); }
    }

    // Using a DependencyProperty as the backing store for Score.
    public static readonly DependencyProperty ScoreProperty =
        DependencyProperty.Register(nameof(Score), typeof(int), typeof(StoryListItemControl), new PropertyMetadata(0));

    public string Title
    {
        get { return (string)GetValue(TitleProperty); }
        set { SetValue(TitleProperty, value); }
    }

    // Using a DependencyProperty as the backing store for Title.
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(StoryListItemControl), new PropertyMetadata(string.Empty));

    public string Url
    {
        get { return (string)GetValue(UrlProperty); }
        set { SetValue(UrlProperty, value); }
    }

    // Using a DependencyProperty as the backing store for Url.
    public static readonly DependencyProperty UrlProperty =
        DependencyProperty.Register(nameof(Url), typeof(string), typeof(StoryListItemControl), new PropertyMetadata(string.Empty));

    public int Descendants
    {
        get { return (int)GetValue(DescendantsProperty); }
        set { SetValue(DescendantsProperty, value); }
    }

    // Using a DependencyProperty as the backing store for Descendants.
    public static readonly DependencyProperty DescendantsProperty =
        DependencyProperty.Register(nameof(Descendants), typeof(int), typeof(StoryListItemControl), new PropertyMetadata(0));

    public string TimeAgo
    {
        get { return (string)GetValue(TimeAgoProperty); }
        set { SetValue(TimeAgoProperty, value); }
    }

    // Using a DependencyProperty as the backing store for TimeAgo.
    public static readonly DependencyProperty TimeAgoProperty =
        DependencyProperty.Register(nameof(TimeAgo), typeof(string), typeof(StoryListItemControl), new PropertyMetadata(string.Empty));

    public string By
    {
        get { return (string)GetValue(ByProperty); }
        set { SetValue(ByProperty, value); }
    }

    // Using a DependencyProperty as the backing store for By.
    public static readonly DependencyProperty ByProperty =
        DependencyProperty.Register(nameof(By), typeof(string), typeof(StoryListItemControl), new PropertyMetadata(string.Empty));

    public bool IsFavorite
    {
        get { return (bool)GetValue(IsFavoriteProperty); }
        set { SetValue(IsFavoriteProperty, value); }
    }

    // Using a DependencyProperty as the backing store for IsFavorite.
    public static readonly DependencyProperty IsFavoriteProperty =
        DependencyProperty.Register(nameof(IsFavorite), typeof(bool), typeof(StoryListItemControl), new PropertyMetadata(false));

    public string RootDomain
    {
        get { return (string)GetValue(RootDomainProperty); }
        set { SetValue(RootDomainProperty, value); }
    }

    // Using a DependencyProperty as the backing store for RootDomain.
    public static readonly DependencyProperty RootDomainProperty =
        DependencyProperty.Register(nameof(RootDomain), typeof(string), typeof(StoryListItemControl), new PropertyMetadata(string.Empty));
}
