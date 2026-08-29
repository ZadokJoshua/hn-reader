using HNReader.Core.Enums;
using HNReader.Core.Interfaces;
using HNReader.Core.Services;
using HNReader.Core.Services.Logging;
using System;

namespace HNReader.Core.Viewmodels;

public partial class AskPageViewModel : PageViewModel
{
    public AskPageViewModel(HNClient client, Lazy<IFavoritesService> favoritesService, HNWebClient webClient, ILogger? logger = null)
        : base(client, favoritesService, StoryType.Ask, webClient, logger)
    {
        PageTitle = "Ask HN";
    }

    public override string EmptyStateTitle => "No Ask HN Posts";

    public override string EmptyStateDescription => "Ask HN posts will appear here when available.";

    public override string EmptyStateGlyph => "\uE9CE";
}
