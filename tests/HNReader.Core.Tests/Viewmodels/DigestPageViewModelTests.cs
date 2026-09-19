using System.Net;
using System.Text;
using HNReader.Core.Enums;
using HNReader.Core.Interfaces;
using HNReader.Core.Services;
using HNReader.Core.Viewmodels;

namespace HNReader.Core.Tests.Viewmodels;

public class DigestPageViewModelTests
{
    // Stamped with today's UTC date so the fixture isn't treated as stale — the
    // stale path is exercised deliberately by its own tests below.
    private static readonly string DigestJson = DigestJsonFor(DateTime.UtcNow);

    private static string DigestJsonFor(DateTime generatedAtUtc) => """
    {
      "success": true,
      "data": {
        "generatedAtUtc": "@STAMP@",
        "categories": [
          {
            "name": "Science & Research",
            "summary": "Science today.",
            "items": [
              { "storyId": 1, "title": "A paper", "url": "https://example.com/a",
                "hackerNewsUrl": "https://news.ycombinator.com/item?id=1",
                "summary": "Summary A", "author": "alice" }
            ]
          },
          {
            "name": "Programming & Software Engineering",
            "summary": "Code today.",
            "items": [
              { "storyId": 2, "title": "A library", "url": "https://example.com/b",
                "hackerNewsUrl": "https://news.ycombinator.com/item?id=2",
                "summary": "Summary B", "author": "bob" },
              { "storyId": 3, "title": "A compiler", "url": null,
                "hackerNewsUrl": "https://news.ycombinator.com/item?id=3",
                "summary": "Summary C", "author": "carol" }
            ]
          }
        ]
      }
    }
    """.Replace("@STAMP@", generatedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"));

    // Taxonomy order deliberately differs from the digest's own order, and lists
    // a category the digest has no stories for.
    private const string TaxonomyJson = """
    {
      "success": true,
      "data": [
        { "name": "Programming & Software Engineering", "isEnabled": true },
        { "name": "Space & Aerospace", "isEnabled": true },
        { "name": "Science & Research", "isEnabled": true }
      ]
    }
    """;

    [Fact]
    public async Task InitializeAsync_WhenDisabled_MakesNoRequestsAndShowsDisabledState()
    {
        var handler = new RoutingHandler();
        var settings = new FakeSettingsService { IsDigestEnabled = false };
        var vm = CreateViewModel(handler, settings);

        await vm.InitializeAsync();

        // The whole point of the setting: no traffic at all to a server the user
        // opted out of.
        Assert.Equal(0, handler.RequestCount);
        Assert.True(vm.ShowDisabledState);
        Assert.False(vm.ShowContent);
        Assert.False(vm.HasLoaded);
    }

    [Fact]
    public async Task InitializeAsync_OrdersCategoriesByTaxonomyAndKeepsEmptyOnes()
    {
        var vm = CreateViewModel(new RoutingHandler(), new FakeSettingsService { IsDigestEnabled = true });

        await vm.InitializeAsync();

        Assert.True(vm.ShowContent);
        Assert.Equal(
            ["Programming & Software Engineering", "Space & Aerospace", "Science & Research"],
            vm.Categories.Select(c => c.Name));

        // A taxonomy category with nothing in today's digest is kept as an empty
        // placeholder rather than silently dropped.
        var empty = vm.Categories.Single(c => c.Name == "Space & Aerospace");
        Assert.False(empty.HasItems);
        Assert.Equal(0, empty.ItemCount);

        Assert.Equal(3, vm.Categories.Sum(c => c.ItemCount));
        Assert.Equal(2, vm.Categories.Single(c => c.Name.StartsWith("Programming")).ItemCount);
    }

    /// <summary>
    /// Opening on an empty section makes a perfectly good digest look broken.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_SelectsFirstCategoryThatHasStories()
    {
        const string taxonomyEmptyFirst = """
        {"success":true,"data":[{"name":"Space & Aerospace","isEnabled":true},
                                {"name":"Science & Research","isEnabled":true}]}
        """;
        var handler = new RoutingHandler(taxonomyJson: taxonomyEmptyFirst);
        var vm = CreateViewModel(handler, new FakeSettingsService { IsDigestEnabled = true });

        await vm.InitializeAsync();

        Assert.Equal("Space & Aerospace", vm.Categories[0].Name);
        Assert.Equal("Science & Research", vm.SelectedCategory?.Name);
    }

    /// <summary>
    /// A category present in the digest but missing from the taxonomy (deleted
    /// since the digest was generated) must not cost the user its stories.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_KeepsDigestCategoriesMissingFromTaxonomy()
    {
        const string partialTaxonomy = """
        {"success":true,"data":[{"name":"Science & Research","isEnabled":true}]}
        """;
        var handler = new RoutingHandler(taxonomyJson: partialTaxonomy);
        var vm = CreateViewModel(handler, new FakeSettingsService { IsDigestEnabled = true });

        await vm.InitializeAsync();

        Assert.Equal(2, vm.Categories.Count);
        Assert.Equal("Science & Research", vm.Categories[0].Name);
        Assert.Equal("Programming & Software Engineering", vm.Categories[1].Name);
        Assert.Equal(3, vm.Categories.Sum(c => c.ItemCount));
    }

    [Fact]
    public async Task InitializeAsync_TaxonomyUnavailable_FallsBackToDigestOrder()
    {
        var handler = new RoutingHandler(taxonomyStatus: HttpStatusCode.InternalServerError);
        var vm = CreateViewModel(handler, new FakeSettingsService { IsDigestEnabled = true });

        await vm.InitializeAsync();

        Assert.True(vm.ShowContent);
        Assert.Equal(
            ["Science & Research", "Programming & Software Engineering"],
            vm.Categories.Select(c => c.Name));
    }

    [Fact]
    public async Task InitializeAsync_NoDigestYet_IsNotAnError()
    {
        var handler = new RoutingHandler(
            digestStatus: HttpStatusCode.NotFound,
            digestJson: """{"success":false,"error":"No digest has been generated yet."}""");
        var vm = CreateViewModel(handler, new FakeSettingsService { IsDigestEnabled = true });

        await vm.InitializeAsync();

        Assert.True(vm.ShowNotGenerated);
        Assert.False(vm.HasError);
        Assert.False(vm.ShowContent);
        Assert.Equal("No digest has been generated yet.", vm.NotGeneratedMessage);

        // Nothing on the server at all is exactly when the app should ask for a
        // digest rather than sit on an empty page.
        Assert.Equal(1, handler.GenerateRequestCount);
    }

    [Fact]
    public async Task InitializeAsync_NetworkFailure_ShowsErrorState()
    {
        var handler = new RoutingHandler(digestStatus: HttpStatusCode.InternalServerError);
        var vm = CreateViewModel(handler, new FakeSettingsService { IsDigestEnabled = true });

        await vm.InitializeAsync();

        Assert.True(vm.ShowError);
        Assert.False(vm.ShowContent);
        Assert.False(string.IsNullOrWhiteSpace(vm.ErrorMessage));
    }

    /// <summary>
    /// Navigating away and back must be free — the digest changes nightly and
    /// the server output-caches it for an hour.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_WithinStalenessWindow_DoesNotRefetch()
    {
        var handler = new RoutingHandler();
        var vm = CreateViewModel(handler, new FakeSettingsService { IsDigestEnabled = true });

        await vm.InitializeAsync();
        var afterFirst = handler.DigestRequestCount;

        await vm.InitializeAsync();

        Assert.Equal(1, afterFirst);
        Assert.Equal(1, handler.DigestRequestCount);
    }

    [Fact]
    public async Task RefreshCommand_AlwaysRefetches()
    {
        var handler = new RoutingHandler();
        var vm = CreateViewModel(handler, new FakeSettingsService { IsDigestEnabled = true });

        await vm.InitializeAsync();
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(2, handler.DigestRequestCount);
    }

    /// <summary>
    /// Turning the feature on while the page is open should populate it rather
    /// than leave the disabled placeholder showing.
    /// </summary>
    [Fact]
    public async Task EnablingTheSetting_LoadsWithoutNavigation()
    {
        var handler = new RoutingHandler();
        var settings = new FakeSettingsService { IsDigestEnabled = false };
        var vm = CreateViewModel(handler, settings);

        await vm.InitializeAsync();
        Assert.Equal(0, handler.RequestCount);

        settings.IsDigestEnabled = true;

        // The handler fires a fire-and-forget load; give it a moment to settle.
        for (var i = 0; i < 50 && handler.DigestRequestCount == 0; i++)
        {
            await Task.Delay(20);
        }

        Assert.Equal(1, handler.DigestRequestCount);
        Assert.False(vm.ShowDisabledState);
    }


    // ── Category selection ───────────────────────────────────────────────

    [Fact]
    public async Task InitializeAsync_BuildsThePickerFromTheServerTaxonomy()
    {
        var vm = CreateViewModel(new RoutingHandler(), new FakeSettingsService { IsDigestEnabled = true });

        await vm.InitializeAsync();

        // The picker comes from the taxonomy endpoint, not from what this digest
        // happened to contain — that is the whole reason the endpoint exists.
        Assert.Equal(
            ["Programming & Software Engineering", "Space & Aerospace", "Science & Research"],
            vm.AvailableCategories.Select(o => o.Name));

        // No stored selection means everything is followed.
        Assert.All(vm.AvailableCategories, o => Assert.True(o.IsSelected));
        Assert.False(vm.IsFiltered);
        Assert.Equal("All categories", vm.CategoryFilterLabel);
    }

    [Fact]
    public async Task InitializeAsync_StoredSelection_TicksOnlyThoseAndShowsOnlyThose()
    {
        var settings = new FakeSettingsService
        {
            IsDigestEnabled = true,
            SelectedDigestCategories = ["Science & Research"]
        };
        var vm = CreateViewModel(new RoutingHandler(), settings);

        await vm.InitializeAsync();

        Assert.True(vm.IsFiltered);
        Assert.Equal("1 of 3 categories", vm.CategoryFilterLabel);
        Assert.Equal(["Science & Research"], vm.Categories.Select(c => c.Name));
        Assert.Single(vm.AvailableCategories.Where(o => o.IsSelected));
    }

    [Fact]
    public async Task InitializeAsync_PassesTheSelectionToTheServer()
    {
        var settings = new FakeSettingsService
        {
            IsDigestEnabled = true,
            SelectedDigestCategories = ["Science & Research"]
        };
        var handler = new RoutingHandler();
        var vm = CreateViewModel(handler, settings);

        await vm.InitializeAsync();

        Assert.Contains("categories=Science%20%26%20Research", handler.LastDigestUri!.Query);
    }

    [Fact]
    public async Task ApplyCategorySelection_PersistsAndReloads()
    {
        var settings = new FakeSettingsService { IsDigestEnabled = true };
        var handler = new RoutingHandler();
        var vm = CreateViewModel(handler, settings);
        await vm.InitializeAsync();

        vm.AvailableCategories.Single(o => o.Name == "Space & Aerospace").IsSelected = false;
        vm.NotifyFilterChanged();
        await vm.ApplyCategorySelectionCommand.ExecuteAsync(null);

        Assert.Equal(2, settings.SelectedDigestCategories.Count);
        Assert.DoesNotContain("Space & Aerospace", settings.SelectedDigestCategories);
        Assert.Equal(2, handler.DigestRequestCount);
        Assert.DoesNotContain("Space & Aerospace", vm.Categories.Select(c => c.Name));
    }

    /// <summary>
    /// Everything ticked is stored as an empty list, not as every name — so a
    /// category added to the taxonomy later is followed automatically instead of
    /// being invisible because it postdates the saved list.
    /// </summary>
    [Fact]
    public async Task ApplyCategorySelection_WithEverythingTicked_StoresEmptyMeaningAll()
    {
        var settings = new FakeSettingsService
        {
            IsDigestEnabled = true,
            SelectedDigestCategories = ["Science & Research"]
        };
        var vm = CreateViewModel(new RoutingHandler(), settings);
        await vm.InitializeAsync();

        vm.SelectAllCategoriesCommand.Execute(null);
        await vm.ApplyCategorySelectionCommand.ExecuteAsync(null);

        Assert.Empty(settings.SelectedDigestCategories);
        Assert.False(vm.IsFiltered);
    }

    [Fact]
    public async Task ApplyCategorySelection_WithNothingTicked_IsRefused()
    {
        var settings = new FakeSettingsService { IsDigestEnabled = true };
        var handler = new RoutingHandler();
        var vm = CreateViewModel(handler, settings);
        await vm.InitializeAsync();

        foreach (var option in vm.AvailableCategories) option.IsSelected = false;
        vm.NotifyFilterChanged();

        Assert.False(vm.CanApplyCategorySelection);

        await vm.ApplyCategorySelectionCommand.ExecuteAsync(null);

        // Nothing persisted and nothing re-fetched: "no categories" is not a
        // state the user can end up stuck in.
        Assert.Empty(settings.SelectedDigestCategories);
        Assert.Equal(1, handler.DigestRequestCount);
    }

    // ── Staleness and next-day generation ────────────────────────────────

    [Fact]
    public async Task InitializeAsync_TodaysDigest_IsNotStaleAndAsksForNothing()
    {
        var handler = new RoutingHandler();
        var vm = CreateViewModel(handler, new FakeSettingsService { IsDigestEnabled = true });

        await vm.InitializeAsync();

        Assert.False(vm.IsStale);
        Assert.False(vm.HasStatusNote);
        Assert.Equal(0, handler.GenerateRequestCount);
    }

    /// <summary>
    /// The next-day case: a digest from yesterday is shown, flagged quietly as
    /// stale, and today's is requested.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_YesterdaysDigest_ShowsItAndRequestsToday()
    {
        var handler = new RoutingHandler(digestJson: DigestJsonFor(DateTime.UtcNow.AddDays(-1)));
        var vm = CreateViewModel(handler, new FakeSettingsService { IsDigestEnabled = true });

        await vm.InitializeAsync();

        Assert.True(vm.IsStale);
        Assert.True(vm.ShowContent);          // yesterday's content stays readable
        Assert.False(vm.HasError);            // and is not an error
        Assert.Equal(1, handler.GenerateRequestCount);
    }

    [Fact]
    public async Task InitializeAsync_StaleDigestAndRunStarted_ReportsGenerating()
    {
        var handler = new RoutingHandler(digestJson: DigestJsonFor(DateTime.UtcNow.AddDays(-1)))
        {
            GenerateOutcome = "Started"
        };
        var vm = CreateViewModel(handler, new FakeSettingsService { IsDigestEnabled = true });

        await vm.InitializeAsync();

        Assert.True(vm.IsGenerating);
        Assert.Equal("Generating today\u0027s digest...", vm.StatusNote);
    }

    /// <summary>
    /// If the server says today's digest is already there, nothing should claim
    /// to be generating — that would be a spinner that never resolves.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_StaleDigestButServerSaysCurrent_DoesNotReportGenerating()
    {
        var handler = new RoutingHandler(digestJson: DigestJsonFor(DateTime.UtcNow.AddDays(-1)))
        {
            GenerateOutcome = "AlreadyCurrent"
        };
        var vm = CreateViewModel(handler, new FakeSettingsService { IsDigestEnabled = true });

        await vm.InitializeAsync();

        Assert.False(vm.IsGenerating);
        Assert.Equal("Yesterday\u0027s digest", vm.StatusNote);
    }

    [Fact]
    public async Task InitializeAsync_WhenDisabled_DoesNotEvenAskForGeneration()
    {
        var handler = new RoutingHandler(digestJson: DigestJsonFor(DateTime.UtcNow.AddDays(-1)));
        var vm = CreateViewModel(handler, new FakeSettingsService { IsDigestEnabled = false });

        await vm.InitializeAsync();

        Assert.Equal(0, handler.RequestCount);
        Assert.Equal(0, handler.GenerateRequestCount);
    }

    private static DigestPageViewModel CreateViewModel(RoutingHandler handler, ISettingsService settings) =>
        new(new DigestClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5282/") }), settings);

    /// <summary>
    /// Answers /digest and /digest/categories separately, and counts each, so a
    /// test can assert exactly how much traffic a state transition produced.
    /// </summary>
    private sealed class RoutingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _digestStatus;
        private readonly string _digestJson;
        private readonly HttpStatusCode _taxonomyStatus;
        private readonly string _taxonomyJson;

        public RoutingHandler(
            HttpStatusCode digestStatus = HttpStatusCode.OK,
            string? digestJson = null,
            HttpStatusCode taxonomyStatus = HttpStatusCode.OK,
            string? taxonomyJson = null)
        {
            _digestStatus = digestStatus;
            _digestJson = digestJson ?? DigestJson;
            _taxonomyStatus = taxonomyStatus;
            _taxonomyJson = taxonomyJson ?? TaxonomyJson;
        }

        public int RequestCount { get; private set; }
        public int DigestRequestCount { get; private set; }
        public int GenerateRequestCount { get; private set; }
        public Uri? LastDigestUri { get; private set; }

        /// <summary>What POST /digest/generate reports back.</summary>
        public string GenerateOutcome { get; set; } = "Started";

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;

            var path = request.RequestUri!.AbsolutePath;

            if (path.Contains("generate", StringComparison.Ordinal))
            {
                GenerateRequestCount++;
                var payload = $"{{\"success\":true,\"data\":{{\"status\":\"{GenerateOutcome}\",\"generatedAtUtc\":null}}}}";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                });
            }

            var isTaxonomy = path.Contains("categories", StringComparison.Ordinal);
            if (!isTaxonomy)
            {
                DigestRequestCount++;
                LastDigestUri = request.RequestUri;
            }

            var status = isTaxonomy ? _taxonomyStatus : _digestStatus;
            var body = isTaxonomy ? _taxonomyJson : _digestJson;

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        private bool _isDigestEnabled;
        private AppTheme _theme = AppTheme.Auto;
        private List<string> _selectedCategories = [];

        public event EventHandler<AppTheme>? ThemeChanged;
        public event EventHandler<bool>? DigestEnabledChanged;
        public event EventHandler? SelectedDigestCategoriesChanged;

        public AppTheme Theme
        {
            get => _theme;
            set
            {
                if (_theme == value) return;
                _theme = value;
                ThemeChanged?.Invoke(this, value);
            }
        }

        public bool IsDigestEnabled
        {
            get => _isDigestEnabled;
            set
            {
                if (_isDigestEnabled == value) return;
                _isDigestEnabled = value;
                DigestEnabledChanged?.Invoke(this, value);
            }
        }

        public IReadOnlyList<string> SelectedDigestCategories
        {
            get => _selectedCategories;
            set
            {
                _selectedCategories = value?.ToList() ?? [];
                SelectedDigestCategoriesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Save() { }

        public void Load() { }
    }
}
