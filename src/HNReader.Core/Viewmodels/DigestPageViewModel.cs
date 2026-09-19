using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HNReader.Core.Constants;
using HNReader.Core.Helpers;
using HNReader.Core.Interfaces;
using HNReader.Core.Models;
using HNReader.Core.Services;
using HNReader.Core.Services.Logging;

namespace HNReader.Core.Viewmodels;

/// <summary>
/// The daily digest page.
///
/// <para>
/// Derives from <see cref="BaseViewModel"/> rather than
/// <see cref="PageViewModel"/>: none of that class's story list, pagination,
/// comment-thread or favourites machinery applies to a digest, and inheriting it
/// would also mean inheriting its automatic <c>PopulateListAsync()</c> call.
/// Loading is opted into through <see cref="IInitializableViewModel"/> instead.
/// </para>
///
/// <para>
/// Registered as a singleton so a loaded digest survives navigation — which is
/// also why it deliberately does <b>not</b> implement <see cref="IDisposable"/>:
/// the navigation service disposes the outgoing page's DataContext, and that
/// would destroy the shared instance on the first navigation away.
/// </para>
/// </summary>
public partial class DigestPageViewModel : BaseViewModel, IInitializableViewModel
{
    private readonly DigestClient _digestClient;
    private readonly ISettingsService _settingsService;
    private readonly ILogger? _logger;

    private DateTimeOffset _lastLoadedAtUtc;
    private CancellationTokenSource? _generationWatchCts;

    public DigestPageViewModel(
        DigestClient digestClient,
        ISettingsService settingsService,
        ILogger? logger = null)
    {
        _digestClient = digestClient;
        _settingsService = settingsService;
        _logger = logger;

        IsDigestEnabled = _settingsService.IsDigestEnabled;
        _settingsService.DigestEnabledChanged += OnDigestEnabledChanged;
    }

    public string PageTitle => "Daily Digest";

    /// <summary>The sections rendered for the current selection.</summary>
    public ObservableCollection<DigestCategorySection> Categories { get; } = [];

    /// <summary>
    /// The whole server taxonomy, with the user's choices. This is what the
    /// category picker binds to — and the reason the taxonomy endpoint exists,
    /// rather than deriving the list from whatever happened to be in one digest.
    /// </summary>
    public ObservableCollection<DigestCategoryOption> AvailableCategories { get; } = [];

    [ObservableProperty]
    private bool isDigestEnabled;

    [ObservableProperty]
    private bool isLoading;

    /// <summary>
    /// A user-initiated refresh, as distinct from <see cref="IsLoading"/>. Bound
    /// to a small spinner on the refresh button so the existing content stays on
    /// screen rather than flashing back to a full-page spinner.
    /// </summary>
    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    /// <summary>The server has no digest at all. Only true before the very first run.</summary>
    [ObservableProperty]
    private bool isNotGenerated;

    [ObservableProperty]
    private string notGeneratedMessage = string.Empty;

    /// <summary>
    /// The server is building today's digest right now. A distinct state from
    /// loading: it lasts minutes, not a moment, and the page keeps showing
    /// yesterday's content underneath while it runs.
    /// </summary>
    [ObservableProperty]
    private bool isGenerating;

    /// <summary>
    /// The digest on screen is from an earlier day. Surfaced as a quiet note
    /// beside the date rather than an error, because stale content is still
    /// perfectly readable — it just isn't today's.
    /// </summary>
    [ObservableProperty]
    private bool isStale;

    [ObservableProperty]
    private DigestCategorySection? selectedCategory;

    [ObservableProperty]
    private string generatedAtDisplay = string.Empty;

    [ObservableProperty]
    private bool hasLoaded;

    public string StatusNote => IsGenerating
        ? "Generating today's digest..."
        : IsStale ? "Yesterday's digest" : string.Empty;

    public bool HasStatusNote => !string.IsNullOrEmpty(StatusNote);

    /// <summary>Whether anything is currently excluded by the picker.</summary>
    public bool IsFiltered => AvailableCategories.Count > 0 && AvailableCategories.Any(c => !c.IsSelected);

    public string CategoryFilterLabel
    {
        get
        {
            if (!IsFiltered) return "All categories";

            var selected = AvailableCategories.Count(c => c.IsSelected);
            return $"{selected} of {AvailableCategories.Count} categories";
        }
    }

    /// <summary>Apply is meaningless with nothing ticked, so the UI disables it.</summary>
    public bool CanApplyCategorySelection => AvailableCategories.Any(c => c.IsSelected);

    public bool ShowDisabledState => !IsDigestEnabled;

    public bool ShowLoading => IsDigestEnabled && IsLoading;

    public bool ShowError => IsDigestEnabled && !IsLoading && HasError;

    public bool ShowNotGenerated => IsDigestEnabled && !IsLoading && !HasError && IsNotGenerated;

    public bool ShowContent => IsDigestEnabled && !IsLoading && !HasError && !IsNotGenerated
                               && Categories.Count > 0;

    public bool ShowEmptyState => IsDigestEnabled && !IsLoading && !HasError && !IsNotGenerated
                                  && HasLoaded && Categories.Count == 0;

    /// <summary>
    /// Loads the digest when the page is navigated to.
    /// <para>
    /// This is the second of the feature's two independent off-switches: the nav
    /// item's visibility hides the entry point, and this returns before touching
    /// the network. Neither relies on the other, so a stale nav item or a
    /// keyboard shortcut can't produce a request to a server the user opted out
    /// of.
    /// </para>
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        IsDigestEnabled = _settingsService.IsDigestEnabled;

        if (!IsDigestEnabled) return;
        if (IsLoading) return;

        // Navigating away and back is free within the staleness window. The
        // digest changes once a night and the server output-caches it for an
        // hour, so re-fetching per navigation would buy nothing.
        if (HasLoaded && DateTimeOffset.UtcNow - _lastLoadedAtUtc < AppFileNames.DIGEST_STALE_AFTER) return;

        await LoadAsync(isUserRefresh: false, cancellationToken);
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync(isUserRefresh: true, CancellationToken.None);

    [RelayCommand]
    private async Task RetryAsync() => await LoadAsync(isUserRefresh: false, CancellationToken.None);

    [RelayCommand]
    private void SelectAllCategories()
    {
        foreach (var option in AvailableCategories) option.IsSelected = true;
        NotifyFilterChanged();
    }

    /// <summary>
    /// Persists the picker's state and reloads with it applied.
    /// <para>
    /// When everything is ticked this stores an <b>empty</b> list rather than
    /// every name: empty means "all", so a category added to the server taxonomy
    /// later is included automatically instead of being invisible because it
    /// wasn't in a list written before it existed.
    /// </para>
    /// </summary>
    [RelayCommand]
    private async Task ApplyCategorySelectionAsync()
    {
        if (!CanApplyCategorySelection) return;

        var selected = AvailableCategories.Where(c => c.IsSelected).Select(c => c.Name).ToList();
        var isEverything = selected.Count == AvailableCategories.Count;

        _settingsService.SelectedDigestCategories = isEverything ? [] : selected;

        await LoadAsync(isUserRefresh: true, CancellationToken.None);
    }

    /// <summary>
    /// Called when a picker checkbox changes, so the label and Apply button track
    /// the pending selection before it is applied.
    /// </summary>
    public void NotifyFilterChanged()
    {
        OnPropertyChanged(nameof(IsFiltered));
        OnPropertyChanged(nameof(CategoryFilterLabel));
        OnPropertyChanged(nameof(CanApplyCategorySelection));
    }

    private async Task LoadAsync(bool isUserRefresh, CancellationToken cancellationToken)
    {
        // A new load supersedes any generation watch still polling from a
        // previous one.
        CancelGenerationWatch();

        if (isUserRefresh) IsRefreshing = true; else IsLoading = true;

        HasError = false;
        ErrorMessage = string.Empty;
        IsNotGenerated = false;
        NotGeneratedMessage = string.Empty;
        NotifyStateChanged();

        try
        {
            var selection = _settingsService.SelectedDigestCategories;

            // Both calls are independent, so they overlap rather than queue. The
            // taxonomy call never throws (it degrades to an empty list), so it
            // needs no separate guard here.
            var taxonomyTask = _digestClient.GetCategoriesAsync(cancellationToken);

            // Intentionally NOT using ConfigureAwait(false): the awaits must
            // resume on the UI thread because the ObservableCollection mutations
            // below run on the dispatcher.
            var result = await _digestClient.GetDigestAsync(selection, cancellationToken);
            var taxonomy = await taxonomyTask;

            SyncAvailableCategories(taxonomy, selection);

            switch (result.Status)
            {
                case DigestStatus.NotGenerated:
                    IsNotGenerated = true;
                    IsStale = false;
                    NotGeneratedMessage = string.IsNullOrWhiteSpace(result.ServerMessage)
                        ? "No digest has been generated yet."
                        : result.ServerMessage!;
                    Categories.Clear();
                    SelectedCategory = null;
                    break;

                case DigestStatus.RateLimited:
                    HasError = true;
                    ErrorMessage = "Too many requests to the digest server. Please try again in a minute.";
                    break;

                default:
                    ApplyDigest(result.Digest!, taxonomy, selection);
                    break;
            }

            HasLoaded = true;
            _lastLoadedAtUtc = DateTimeOffset.UtcNow;

            // Whatever we ended up showing, if it isn't today's then ask the
            // server for today's. This is what makes the app self-updating on the
            // first launch of a new day.
            if (IsStale || IsNotGenerated)
            {
                await RequestTodaysDigestAsync();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError("Digest", "failed to load the digest", ex);
            HasError = true;

            // ErrorMessageHelper.GetUserMessage maps every HttpRequestException to
            // "check your internet connection" — right for HNClient/HNWebClient,
            // which talk to real internet hosts, but misleading here: this
            // exception is far more likely a refused connection to the local
            // digest server (not running, wrong port) than an internet outage,
            // and the user can usually tell the difference themselves because
            // every other page still loads fine over the same connection.
            ErrorMessage = ex is HttpRequestException
                ? "Could not reach the digest server. Make sure HNReader.Server is running, then try again."
                : ErrorMessageHelper.GetUserMessage(ex);
        }
        finally
        {
            IsLoading = false;
            IsRefreshing = false;
            NotifyStateChanged();
        }
    }

    /// <summary>
    /// Asks the server to build today's digest, and if it says a run is under way
    /// starts watching for the result.
    /// <para>
    /// Safe to call whenever the digest looks out of date: the server starts a run
    /// only when there is genuinely no digest for the current UTC day and none
    /// already running, so this cannot cost more than the nightly job.
    /// </para>
    /// </summary>
    private async Task RequestTodaysDigestAsync()
    {
        var status = await _digestClient.RequestGenerationAsync();

        if (status is null || status.Status == "AlreadyCurrent")
        {
            return;
        }

        IsGenerating = true;
        NotifyStateChanged();
        StartGenerationWatch();
    }

    /// <summary>
    /// Polls until today's digest appears, then loads it. Bounded by a timeout so
    /// a run that dies doesn't leave the page claiming to be generating forever.
    /// </summary>
    private void StartGenerationWatch()
    {
        var cts = new CancellationTokenSource();
        _generationWatchCts = cts;
        var token = cts.Token;

        _ = Task.Run(async () =>
        {
            var deadline = DateTimeOffset.UtcNow + AppFileNames.DIGEST_GENERATION_TIMEOUT;

            try
            {
                while (!token.IsCancellationRequested && DateTimeOffset.UtcNow < deadline)
                {
                    await Task.Delay(AppFileNames.DIGEST_GENERATION_POLL_INTERVAL, token)
                        .ConfigureAwait(false);

                    var result = await _digestClient
                        .GetDigestAsync(_settingsService.SelectedDigestCategories, token)
                        .ConfigureAwait(false);

                    if (result.Status != DigestStatus.Ok || result.Digest is null) continue;
                    if (result.Digest.GeneratedAtUtc.Date != DateTime.UtcNow.Date) continue;

                    // Today's digest has landed. Hand the actual state mutation
                    // back through the normal load path rather than touching
                    // bound collections from this thread.
                    IsGenerating = false;
                    await LoadOnUiAsync().ConfigureAwait(false);
                    return;
                }

                if (!token.IsCancellationRequested)
                {
                    _logger?.LogWarning("Digest", "gave up waiting for today's digest to be generated");
                    IsGenerating = false;
                    NotifyStateChanged();
                }
            }
            catch (OperationCanceledException)
            {
                // Superseded by a newer load — nothing to do.
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("Digest", "generation watch failed", ex);
                IsGenerating = false;
                NotifyStateChanged();
            }
        }, token);
    }

    // Kept as its own method so the polling loop has one obvious way back into
    // the normal load path, and so a test can await the same thing.
    private Task LoadOnUiAsync() => LoadAsync(isUserRefresh: true, CancellationToken.None);

    private void CancelGenerationWatch()
    {
        var cts = _generationWatchCts;
        _generationWatchCts = null;

        if (cts is null) return;

        try { cts.Cancel(); } catch { /* already disposed */ }
        cts.Dispose();
    }

    /// <summary>
    /// Rebuilds the picker from the server taxonomy, preserving the user's
    /// choices. An empty stored selection means "everything", so every box is
    /// ticked.
    /// </summary>
    private void SyncAvailableCategories(IReadOnlyList<string> taxonomy, IReadOnlyList<string> selection)
    {
        if (taxonomy.Count == 0) return;

        var selectAll = selection.Count == 0;
        var selectedSet = selection.ToHashSet(StringComparer.OrdinalIgnoreCase);

        AvailableCategories.Clear();
        foreach (var name in taxonomy)
        {
            AvailableCategories.Add(new DigestCategoryOption(name, selectAll || selectedSet.Contains(name)));
        }

        NotifyFilterChanged();
    }

    private void ApplyDigest(
        Shared.Models.DigestDto digest,
        IReadOnlyList<string> taxonomy,
        IReadOnlyList<string> selection)
    {
        var sections = digest.Categories.Select(DigestCategorySection.From).ToList();
        var byName = sections.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

        // Which categories to show: the user's selection when they have one, else
        // the whole taxonomy. Either way the *order* comes from the taxonomy,
        // which is the stable, server-defined ordering.
        var wanted = selection.Count == 0
            ? taxonomy
            : taxonomy.Where(n => selection.Contains(n, StringComparer.OrdinalIgnoreCase)).ToList();

        Categories.Clear();

        foreach (var name in wanted)
        {
            Categories.Add(byName.TryGetValue(name, out var section)
                ? section
                : DigestCategorySection.Empty(name));
        }

        // A category the digest has but the taxonomy doesn't (deleted since this
        // digest was generated) is appended rather than discarded — dropping real
        // stories to satisfy an ordering would be the wrong trade.
        //
        // But it must still pass the user's filter. An earlier version appended
        // unconditionally, which quietly resurrected every category the user had
        // just unticked: the server does filter, so this only showed up under a
        // client-side test, and relying on the server to enforce the user's own
        // choice is the wrong place for that guarantee to live.
        var listed = wanted.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var permitted = selection.Count == 0
            ? null
            : selection.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var section in sections.Where(s =>
                     !listed.Contains(s.Name) && (permitted is null || permitted.Contains(s.Name))))
        {
            Categories.Add(section);
        }

        // The first category with stories, not merely the first: opening on an
        // empty section makes a perfectly good digest look broken.
        SelectedCategory = Categories.FirstOrDefault(c => c.HasItems) ?? Categories.FirstOrDefault();

        // LiteDB can round-trip a DateTime back as Unspecified, in which case the
        // JSON carries no 'Z' and it parses as Unspecified too. Stamping UTC
        // before converting is what stops the displayed date drifting by the
        // local UTC offset — and why this is a pre-formatted string rather than a
        // DateTime bound through RelativeTimeConverter.
        var generatedUtc = DateTime.SpecifyKind(digest.GeneratedAtUtc, DateTimeKind.Utc);
        GeneratedAtDisplay = generatedUtc.ToLocalTime().ToString("d MMM yyyy");

        // Compared in UTC because that is the day boundary the server's nightly
        // cron runs on; using local dates would make two clients in different
        // zones disagree about whether the same digest was current.
        IsStale = generatedUtc.Date != DateTime.UtcNow.Date;
    }

    private void OnDigestEnabledChanged(object? sender, bool enabled)
    {
        IsDigestEnabled = enabled;

        // Turning the feature on while the page is open should populate it
        // rather than leave the user looking at the disabled placeholder.
        if (enabled && !HasLoaded)
        {
            _ = LoadAsync(isUserRefresh: false, CancellationToken.None);
        }
    }

    // The visibility flags above are computed from several observable properties
    // and from Categories.Count, which changes via Add()/Clear() calls that raise
    // nothing for them. Re-announcing the whole set after any state transition is
    // simpler and more reliable than wiring per-property fan-out, and follows
    // PageViewModel.NotifyPaginationPropertiesChanged.
    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(ShowDisabledState));
        OnPropertyChanged(nameof(ShowLoading));
        OnPropertyChanged(nameof(ShowError));
        OnPropertyChanged(nameof(ShowNotGenerated));
        OnPropertyChanged(nameof(ShowContent));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(StatusNote));
        OnPropertyChanged(nameof(HasStatusNote));
    }

    partial void OnIsDigestEnabledChanged(bool value) => NotifyStateChanged();

    partial void OnHasLoadedChanged(bool value) => NotifyStateChanged();

    partial void OnIsStaleChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusNote));
        OnPropertyChanged(nameof(HasStatusNote));
    }

    partial void OnIsGeneratingChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusNote));
        OnPropertyChanged(nameof(HasStatusNote));
    }
}
