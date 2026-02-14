using HNReader.Core.Enums;
using HNReader.Core.Interfaces;
using HNReader.Core.Services;

namespace HNReader.Core.Viewmodels;

public partial class TopPageViewModel : PageViewModel
{
    public TopPageViewModel(HNClient client, IFavoritesService favoritesService, ISettingsService settingsService, HNWebClient webClient, IContentScraperService contentScraperService, CopilotCliService copilotCliService, IVaultFileService vaultFileService)
        : base(client, favoritesService, StoryType.Top, contentScraperService, settingsService, webClient, copilotCliService, vaultFileService)
    {
        PageTitle = "Top Stories";
    }
}
