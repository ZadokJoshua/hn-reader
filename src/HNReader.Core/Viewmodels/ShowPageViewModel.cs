using HNReader.Core.Enums;
using HNReader.Core.Interfaces;
using HNReader.Core.Services;
using System;

namespace HNReader.Core.Viewmodels;

public partial class ShowPageViewModel : PageViewModel
{
    public ShowPageViewModel(HNClient client, Lazy<IFavoritesService> favoritesService, HNWebClient webClient)
        : base(client, favoritesService, StoryType.Show, webClient)
    {
        PageTitle = "Show HN";
    }

    public override string EmptyStateTitle => "No Show HN Posts";

    public override string EmptyStateDescription => "Show HN posts will appear here when available.";

    public override string EmptyStateGlyph => "\uE943";
}
