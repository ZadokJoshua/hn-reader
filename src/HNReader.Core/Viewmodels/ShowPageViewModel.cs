using HNReader.Core.Enums;
using HNReader.Core.Interfaces;
using HNReader.Core.Services;

namespace HNReader.Core.Viewmodels;

public partial class ShowPageViewModel : PageViewModel
{
    public ShowPageViewModel(HNClient client, IFavoritesService favoritesService, ISettingsService settingsService, HNWebClient webClient, IContentScraperService contentScraperService, CopilotCliService copilotCliService, IVaultFileService vaultFileService)
        : base(client, favoritesService, StoryType.Show, contentScraperService, settingsService, webClient, copilotCliService, vaultFileService)
    {
        PageTitle = "Show HN";
    }

    public override string EmptyStateTitle => "No Show HN Posts";

    public override string EmptyStateDescription => "Show HN posts will appear here when available.";

    public override string EmptyStateGlyph => "\uE943";
}
