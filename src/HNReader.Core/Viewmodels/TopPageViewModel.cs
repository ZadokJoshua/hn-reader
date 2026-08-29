using HNReader.Core.Enums;
using HNReader.Core.Interfaces;
using HNReader.Core.Services;
using HNReader.Core.Services.Logging;
using System;

namespace HNReader.Core.Viewmodels;

public partial class TopPageViewModel : PageViewModel
{
    public TopPageViewModel(HNClient client, Lazy<IFavoritesService> favoritesService, HNWebClient webClient, ILogger? logger = null)
        : base(client, favoritesService, StoryType.Top, webClient, logger)
    {
        PageTitle = "Top Stories";
    }
}
