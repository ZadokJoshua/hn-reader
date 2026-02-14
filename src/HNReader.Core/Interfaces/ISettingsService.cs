using HNReader.Core.Enums;
using HNReader.Core.Models;

namespace HNReader.Core.Interfaces;

public interface ISettingsService
{
    AppTheme Theme { get; set; }
    int StoryLimit { get; set; }
    List<Interest> UserInterests { get; set; }
    int MaxStoriesPerDigestGroup { get; set; }
    string CopilotModel { get; set; }
    
    // AI Vault settings
    string? VaultPath { get; set; }
    string? VaultName { get; }
    bool HasVault { get; }
    string GetKnowledgeBasePath(KnowledgeBaseFolders folder);

    event EventHandler<AppTheme>? ThemeChanged;
    event EventHandler<string?>? VaultChanged;
    event EventHandler<List<Interest>>? UserInterestsChanged;
    
    void Save();
    void Load();
}
