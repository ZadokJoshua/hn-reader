using HNReader.Core.Enums;
using HNReader.Core.Interfaces;
using HNReader.Core.Services;
using System;

namespace HNReader.Core.Viewmodels;

public partial class BestPageViewModel : PageViewModel
{
    public BestPageViewModel(HNClient client, Lazy<IFavoritesService> favoritesService, HNWebClient webClient)
        : base(client, favoritesService, StoryType.Best, webClient)
    {
        PageTitle = "Best Stories";
    }
}
