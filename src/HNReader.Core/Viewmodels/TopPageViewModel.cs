using HNReader.Core.Enums;
using HNReader.Core.Interfaces;
using HNReader.Core.Services;
using System;

namespace HNReader.Core.Viewmodels;

public partial class TopPageViewModel : PageViewModel
{
    public TopPageViewModel(HNClient client, Lazy<IFavoritesService> favoritesService, HNWebClient webClient)
        : base(client, favoritesService, StoryType.Top, webClient)
    {
        PageTitle = "Top Stories";
    }
}
