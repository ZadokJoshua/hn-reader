using HNReader.Core.Enums;
using HNReader.Core.Interfaces;
using HNReader.Core.Services;

namespace HNReader.Core.Viewmodels;

public partial class AskPageViewModel : PageViewModel
{
    public AskPageViewModel(HNClient client, IFavoritesService favoritesService, ISettingsService settingsService, HNWebClient webClient, IContentScraperService contentScraperService, CopilotCliService copilotCliService, IVaultFileService vaultFileService)
        : base(client, favoritesService, StoryType.Ask, contentScraperService, settingsService, webClient, copilotCliService, vaultFileService)
    {
        PageTitle = "Ask HN";
    }

    public override string EmptyStateTitle => "No Ask HN Posts";

    public override string EmptyStateDescription => "Ask HN posts will appear here when available.";

    public override string EmptyStateGlyph => "\uE9CE";
}
