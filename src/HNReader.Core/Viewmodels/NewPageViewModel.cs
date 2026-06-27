using HNReader.Core.Enums;
using HNReader.Core.Interfaces;
using HNReader.Core.Services;
using System;

namespace HNReader.Core.Viewmodels;

public partial class NewPageViewModel : PageViewModel
{
    public NewPageViewModel(HNClient client, Lazy<IFavoritesService> favoritesService, HNWebClient webClient)
        : base(client, favoritesService, StoryType.New, webClient)
    {
        PageTitle = "New Stories";
    }
}
