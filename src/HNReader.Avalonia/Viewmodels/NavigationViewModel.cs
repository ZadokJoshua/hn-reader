using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HNReader.Avalonia.Models;
using HNReader.Avalonia.Services;
using HNReader.Core.Enums;

namespace HNReader.Avalonia.Viewmodels;

/// <summary>
/// Backs the navigation pane: which items exist, which one is selected, and
/// whether the pane is expanded.
/// </summary>
/// <remarks>
/// Lives in the Avalonia project because it backs this shell's observable
/// navigation pane.
///
/// Selection is strictly one-way: the pane raises <see cref="NavigateCommand"/>,
/// NavigationService decides, and <see cref="OnNavigated"/> projects the result
/// back onto the items. Nothing here observes a selection change, so there is no
/// feedback loop to guard against.
/// </remarks>
public partial class NavigationViewModel : ObservableObject, IDisposable
{
    private readonly NavigationService _navigation;

    public ObservableCollection<NavigationItem> BrowseItems { get; }
    public ObservableCollection<NavigationItem> PersonalItems { get; }
    public ObservableCollection<NavigationItem> FooterItems { get; }

    [ObservableProperty]
    private bool _isPaneOpen = true;

    public NavigationViewModel(NavigationService navigation)
    {
        _navigation = navigation;

        BrowseItems =
        [
            new NavigationItem(ApplicationPages.Top, "Top Stories", "IconTop"),
            new NavigationItem(ApplicationPages.New, "New Stories", "IconClock"),
            new NavigationItem(ApplicationPages.Best, "Best Stories", "IconTrophy"),
            new NavigationItem(ApplicationPages.Show, "Show HN", "IconRocket"),
            new NavigationItem(ApplicationPages.Ask, "Ask HN", "IconAsk"),
        ];

        PersonalItems =
        [
            new NavigationItem(ApplicationPages.Favourites, "Favorites", "IconHeart"),
        ];

        FooterItems =
        [
            new NavigationItem(ApplicationPages.Settings, "Settings", "IconGear"),
        ];

        _navigation.Navigated += OnNavigated;
    }

    /// <summary>
    /// Every item in the order the pane renders them.
    /// </summary>
    private IEnumerable<NavigationItem> AllItems =>
        BrowseItems.Concat(PersonalItems).Concat(FooterItems);

    [RelayCommand]
    private void Navigate(NavigationItem? item)
    {
        if (item is null) return;
        _navigation.NavigateToPage(item.Page);
    }

    [RelayCommand]
    private void TogglePane() => IsPaneOpen = !IsPaneOpen;

    /// <summary>
    /// Projects the page NavigationService actually moved to onto the items.
    /// </summary>
    private void OnNavigated(ApplicationPages page)
    {
        foreach (var item in AllItems)
        {
            item.IsSelected = item.Page == page;
        }
    }

    /// <summary>
    /// Navigates to the nth visible item, counting down the pane as drawn.
    /// </summary>
    public bool TryNavigateByOrdinal(int oneBasedIndex)
    {
        var visible = BrowseItems.Concat(PersonalItems).Where(i => i.IsVisible).ToList();
        if (oneBasedIndex < 1 || oneBasedIndex > visible.Count) return false;

        _navigation.NavigateToPage(visible[oneBasedIndex - 1].Page);
        return true;
    }

    // NavigationService is a singleton that outlives the window this view model
    // belongs to, so its subscription must be released.
    public void Dispose()
    {
        _navigation.Navigated -= OnNavigated;
        GC.SuppressFinalize(this);
    }
}
