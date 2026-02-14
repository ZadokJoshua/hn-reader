using HNReader.Core.Enums;
using HNReader.Core.Models;
using HNReader.Core.Viewmodels;
using HNReader.WinUI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.ComponentModel;
using System.Threading.Tasks;

namespace HNReader.WinUI.Views;

public sealed partial class NewsDigestPage : Page
{
    private readonly NewsDigestViewModel _viewModel;
    private bool _isInitializing;

    public NewsDigestPage(NewsDigestViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        Loaded += NewsDigestPage_Loaded;
    }

    private async void NewsDigestPage_Loaded(object sender, RoutedEventArgs e)
    {
        await InitializeDigestAsync();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await InitializeDigestAsync();
    }

    private async Task InitializeDigestAsync()
    {
        if (_isInitializing) return;
        _isInitializing = true;

        _viewModel.UpdateInterestsPreview();

        try
        {
            // Try to load an existing digest from the vault when appropriate
            await _viewModel.LoadExistingDigestAsync();
            BuildSegmentedControlItems();
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private void ManageInterestsButton_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current is App app)
        {
            var navigationService = app.Services.GetService<NavigationService>();
            navigationService?.NavigateToPage(ApplicationPages.Settings);
        }
    }

    private void ErrorInfoBar_Closed(InfoBar sender, InfoBarClosedEventArgs args)
    {
        if (_viewModel != null)
        {
            _viewModel.HasError = false;
        }
    }

    private void SegmentedControl_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton radioButton)
        {
            if (radioButton.Tag is string tag && tag == "Overview")
            {
                _viewModel.SelectGroup(null);
            }
            else if (radioButton.Tag is DigestInterestGroup group)
            {
                try
                {
                    _viewModel.SelectGroup(group);
                }
                catch (System.Exception)
                {
                    throw;
                }
            }
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(_viewModel.HasDigest):
            case nameof(_viewModel.DigestGroups):
                DispatcherQueue.TryEnqueue(BuildSegmentedControlItems);
                break;
        }
    }

    private void BuildSegmentedControlItems()
    {
        SegmentedControlPanel.Children.Clear();
        
        // Add Overview item without story count
        var overviewButton = CreateSegmentButton("Overview", null, "Overview");
        overviewButton.IsChecked = _viewModel.IsOverviewSelected;
        SegmentedControlPanel.Children.Add(overviewButton);

        // Add interest group items with story counts
        foreach (var group in _viewModel.DigestGroups)
        {
            var button = CreateSegmentButton(group.Interest.Name, group.StoryCount, group);
            button.IsChecked = (_viewModel.SelectedGroup == group);
            SegmentedControlPanel.Children.Add(button);
        }
    }
    
    private RadioButton CreateSegmentButton(string title, int? storyCount, object tag)
    {
        var radioButton = new RadioButton
        {
            Tag = tag,
            GroupName = "DigestSegments",
            Style = (Style)this.Resources["SegmentedRadioButtonStyle"],
            Content = CreateSegmentContent(title, storyCount)
        };
        radioButton.Checked += SegmentedControl_Checked;
        return radioButton;
    }
    
    private UIElement CreateSegmentContent(string title, int? storyCount)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        
        // Title text
        var titleText = new TextBlock
        {
            Text = title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(titleText);
        
        // Story count badge (only if count is provided and > 0)
        if (storyCount.HasValue && storyCount.Value > 0)
        {
            var badge = new Border
            {
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(6, 2, 6, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = storyCount.Value.ToString(),
                    FontSize = 11,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                }
            };
            panel.Children.Add(badge);
        }
        
        return panel;
    }
}
