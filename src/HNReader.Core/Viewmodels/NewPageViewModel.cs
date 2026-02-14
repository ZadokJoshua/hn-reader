using HNReader.Core.Enums;
using HNReader.Core.Interfaces;
using HNReader.Core.Services;

namespace HNReader.Core.Viewmodels;

public partial class NewPageViewModel : PageViewModel
{
    public NewPageViewModel(HNClient client, IFavoritesService favoritesService, ISettingsService settingsService, HNWebClient webClient, IContentScraperService contentScraperService, CopilotCliService copilotCliService, IVaultFileService vaultFileService)
        : base(client, favoritesService, StoryType.New, contentScraperService, settingsService, webClient, copilotCliService, vaultFileService)
    {
        PageTitle = "New Stories";
    }
}
