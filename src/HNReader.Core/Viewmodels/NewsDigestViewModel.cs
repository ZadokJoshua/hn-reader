using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HNReader.Core.Constants;
using HNReader.Core.Enums;
using HNReader.Core.Helpers;
using HNReader.Core.Interfaces;
using HNReader.Core.Models;
using HNReader.Core.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;

namespace HNReader.Core.Viewmodels;

public partial class NewsDigestViewModel : BaseViewModel
{
    private readonly ISettingsService _newsDigestSettingsService;
    private readonly CopilotCliService _copilotCliService;
    private readonly IVaultFileService _vaultFileService;
    private readonly IContentScraperService _contentScraperService;
    private readonly HNClient _hnClient;
    private bool _hasCheckedExistingDigest;

    private static readonly JsonSerializerOptions _indentedJsonOptions = new()
    {
        WriteIndented = true
    };

    public NewsDigestViewModel(
        ISettingsService settingsService,
        CopilotCliService copilotCliService,
        IVaultFileService vaultFileService,
        IContentScraperService contentScraperService,
        HNClient hnClient)
    {
        _newsDigestSettingsService = settingsService;
        _copilotCliService = copilotCliService;
        _vaultFileService = vaultFileService;
        _contentScraperService = contentScraperService;
        _digestGroups = [];
        _selectedGroupStories = [];

        UpdateInterestsPreview();

        _hnClient = hnClient;

        _newsDigestSettingsService.UserInterestsChanged += OnUserInterestsChanged;
    }

    private void OnUserInterestsChanged(object? sender, List<Interest> interests)
    {
        UpdateInterestsPreview();
    }

    [ObservableProperty]
    private ObservableCollection<DigestInterestGroup> _digestGroups;

    [ObservableProperty]
    private ObservableCollection<HNHit> _selectedGroupStories;

    [ObservableProperty]
    private DigestInterestGroup? _selectedGroup;

    [ObservableProperty]
    private string _digestDate = string.Empty;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private bool _hasDigest;

    [ObservableProperty]
    private bool _isOverviewSelected = true;

    [ObservableProperty]
    private int _loadingProgress;

    [ObservableProperty]
    private string _loadingMessage = string.Empty;

    [ObservableProperty]
    private string _interestsPreview = "No interests configured yet";

    [ObservableProperty]
    private bool _hasInterests;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task GenerateDigestAsync(CancellationToken ct)
    {
        if (_copilotCliService is null) return;

        _hasCheckedExistingDigest = false;
        IsGenerating = true;
        HasError = false;
        ErrorMessage = string.Empty;
        HasDigest = false;
        DigestGroups.Clear();
        SelectedGroupStories.Clear();
        SelectedGroup = null;
        IsOverviewSelected = true;
        SummaryText = string.Empty;
        DigestDate = string.Empty;

        try
        {
            IProgress<DigestGenerationProgress> progress = new Progress<DigestGenerationProgress>(OnProgressReported);
            progress?.Report(DigestGenerationProgress.Stage(2, "Initializing knowledge base..."));

            await _vaultFileService.InitializeKnowledgeBaseAsync();
            Debug.WriteLine("[DigestGen] Knowledge base initialized");

            progress?.Report(DigestGenerationProgress.Stage(5, "Fetching trending stories from Hacker News..."));
            ct.ThrowIfCancellationRequested();

            var searchResult = await _hnClient.GetStoriesFromLast24HoursAsync();
            var storyCount = searchResult.Hits.Count;

            progress?.Report(DigestGenerationProgress.Stage(15, $"Found {storyCount} trending stories"));
            ct.ThrowIfCancellationRequested();

            // Serialize raw hits to JSON and save to the knowledge base
            var rawJson = JsonSerializer.Serialize(searchResult.Hits, _indentedJsonOptions);
            var digestFolder = KnowledgeBaseFolders.NewsDigest.GetDescription();
            var rawDataPath = Path.Combine(digestFolder, AppFileNames.UNPROCESSED_DIGEST_DATA_FILE_NAME);
            await _vaultFileService.WriteTextAsync(rawDataPath, rawJson);

            progress?.Report(DigestGenerationProgress.Stage(30,
                $"Saved {storyCount} stories to knowledge base"));
            ct.ThrowIfCancellationRequested();

            progress?.Report(DigestGenerationProgress.Stage(35, "Starting AI agent..."));

            await _copilotCliService.GenerateNewsDigestAsync(progress, ct);

            var digest = await _vaultFileService.LoadDigestAsync();
            
            if (digest is not null)
            {
                // Enrich digest with timestamp and cached image URLs
                progress?.Report(DigestGenerationProgress.Stage(95, "Enriching stories with images..."));
                digest.GeneratedAt = DateTime.UtcNow;
                foreach (var group in digest.Groups)
                {
                    foreach (var story in group.Stories)
                    {
                        if (!string.IsNullOrEmpty(story.Url) &&
                            _contentScraperService.TryGetCachedImageUrl(story.Url, out var imgUrl))
                        {
                            story.ImageUrl = SanitizeImageUrl(imgUrl);
                        }
                        else
                        {
                            story.ImageUrl = SanitizeImageUrl(story.ImageUrl);
                        }
                    }
                }

                // Persist enriched digest back to file
                await _vaultFileService.SaveDigestAsync(digest);
                progress?.Report(DigestGenerationProgress.Complete(digest));
                PopulateFromDto(digest);
            }
        }
        catch (OperationCanceledException)
        {
            LoadingMessage = "Generation cancelled.";
            HasDigest = false;
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
            LoadingMessage = $"Error: {ex.Message}";
            HasDigest = false;
        }
        finally
        {
            IsGenerating = false;
        }
    }

    public async Task LoadExistingDigestAsync()
    {
        if (_vaultFileService is null) return;
        if (IsGenerating || HasDigest || _hasCheckedExistingDigest) return;

        try
        {
            _hasCheckedExistingDigest = true;
            var dto = await _vaultFileService.LoadDigestAsync();
            if (dto is not null) PopulateFromDto(dto);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading existing digest: {ex.Message}");
        }
    }

    /// <summary>
    /// Called from code-behind when the SelectorBar selection changes.
    /// Pass null for the "Overview" tab.
    /// </summary>
    public void SelectGroup(DigestInterestGroup? group)
    {
        SelectedGroup = group;
        IsOverviewSelected = group is null;

        if (group is not null)
        {
            SelectedGroupStories = new ObservableCollection<HNHit>(group.Stories);
        }
        else
        {
            SelectedGroupStories.Clear();
        }
    }

    public void UpdateInterestsPreview()
    {
        var interests = _newsDigestSettingsService?.UserInterests ?? [];
        if (interests.Count > 0)
        {
            var names = interests.Select(i => i.Name).ToArray();
            InterestsPreview = $"Your interests: {string.Join(", ", names)}";
            HasInterests = true;
        }
        else
        {
            InterestsPreview = "No interests configured yet";
            HasInterests = false;
        }
    }

    private void OnProgressReported(DigestGenerationProgress p)
    {
        LoadingProgress = p.Percentage;
        LoadingMessage = p.Message;

        if (p.HasError)
        {
            HasError = true;
            ErrorMessage = p.ErrorMessage ?? "Unknown error";
        }
    }

    /// <summary>
    /// Populates the ViewModel state from a deserialized <see cref="DigestOutputDto"/>.
    /// </summary>
    private void PopulateFromDto(DigestOutputDto dto)
    {
        SummaryText = dto.Summary;
        DigestDate = FormatDigestDate(dto.GeneratedAt);

        var groups = new ObservableCollection<DigestInterestGroup>();

        foreach (var groupDto in dto.Groups)
        {
            var interest = Interest.Create(groupDto.Interest, groupDto.InterestDescription);
            var group = new DigestInterestGroup(interest) { Summary = groupDto.Summary };

            foreach (var storyDto in groupDto.Stories)
            {
                var hit = new HNHit
                {
                    Id = storyDto.Id,
                    Title = storyDto.Title,
                    Url = storyDto.Url,
                    Author = storyDto.Author,
                    Summary = storyDto.Summary,
                    ImageUrl = SanitizeImageUrl(storyDto.ImageUrl),
                    CreatedAt = DateTime.TryParse(storyDto.CreatedAt, out var dt) ? dt : DateTime.MinValue
                };
                group.Stories.Add(hit);
            }

            groups.Add(group);
        }

        DigestGroups = groups;
        HasDigest = true;
        IsOverviewSelected = true;
        SelectedGroup = null;
    }

    private static string FormatDigestDate(DateTime? rawDate)
    {
        if (rawDate.HasValue)
        {
            var local = rawDate.Value.ToLocalTime();
            return $"Generated on {local:MMMM dd, yyyy 'at' h:mm tt}";
        }

        return string.Empty;
    }

    private static string? SanitizeImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
               || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            ? uri.ToString()
            : null;
    }
}
