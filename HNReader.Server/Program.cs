using Hangfire;
using Hangfire.Dashboard;
using Hangfire.Storage.SQLite;
using HNReader.Core.Services;
using HNReader.Server;
using HNReader.Server.Configuration;
using HNReader.Server.Services;
using HNReader.Shared.Models;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.Configure<OpenAIOptions>(options =>
{
    builder.Configuration.GetSection("OpenAI").Bind(options);

    // Fall back to the standard OPENAI_API_KEY env var convention (used by
    // OpenAI's own SDKs/tools) if OpenAI:ApiKey / OpenAI__ApiKey wasn't set.
    if (string.IsNullOrWhiteSpace(options.ApiKey))
    {
        options.ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty;
    }
});
// A plain binding again: the Categories list that needed a Clear() workaround
// (ConfigurationBinder appends to non-empty List<T> defaults rather than
// replacing them, which silently doubled the taxonomy) now lives in the
// database, and every remaining option is a scalar.
builder.Services.Configure<DigestOptions>(builder.Configuration.GetSection("Digest"));

builder.Services.Configure<AdminOptions>(options =>
{
    builder.Configuration.GetSection("Admin").Bind(options);

    // Same convention as OpenAI:ApiKey above — a bare environment variable is
    // accepted as a fallback so a deployment doesn't have to know about the
    // double-underscore section syntax.
    if (string.IsNullOrWhiteSpace(options.ApiKey))
    {
        options.ApiKey = Environment.GetEnvironmentVariable("ADMIN_API_KEY") ?? string.Empty;
    }
});
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));

var storageOptions = builder.Configuration.GetSection("Storage").Get<StorageOptions>() ?? new StorageOptions();
Directory.CreateDirectory(storageOptions.DataDirectory);
var digestDbPath = Path.Combine(storageOptions.DataDirectory, "digests.db");
var hangfireDbPath = Path.Combine(storageOptions.DataDirectory, "hangfire.db");

builder.Services.AddHttpClient<AlgoliaSearchService>(client =>
{
    client.BaseAddress = new Uri("https://hn.algolia.com/api/v1/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddHttpClient<HNWebClient>(client =>
{
    client.BaseAddress = new Uri("https://news.ycombinator.com/");
    // 30s, not the 15s used for the (small, fast) Algolia JSON endpoint: HN
    // comment pages are full HTML threads that can run to megabytes, and a
    // measured digest run timed out on 7 of 18 read_comments fetches at 15s.
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("HNReader.Server/1.0");
});

// Preview-image lookups hit arbitrary third-party article hosts, so this client
// gets its own short timeout and a redirect allowance rather than sharing the
// HN/Algolia clients' settings. Compression is enabled because it only ever
// reads the first chunk of an HTML head.
builder.Services.AddHttpClient<StoryImageService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("HNReader.Server/1.0 (+preview-image)");
    client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml");
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AllowAutoRedirect = true,
    MaxAutomaticRedirections = 5,
    AutomaticDecompression = System.Net.DecompressionMethods.All
});

builder.Services.AddSingleton(_ => new DigestStorageService(digestDbPath));

// Singleton: it caches the taxonomy in memory for the /digest hot path, and the
// scoped DigestAIService reads its snapshot once per run.
builder.Services.AddSingleton<DigestCategoryProvider>();
builder.Services.AddScoped<ArticleScrapingService>();
builder.Services.AddScoped<CommentsReadingService>();
builder.Services.AddScoped<DigestAIService>();
builder.Services.AddScoped<DigestJob>();

// Singleton: it holds the "a run is in flight" flag that makes client-requested
// generation idempotent across requests.
builder.Services.AddSingleton<DigestGenerationCoordinator>();

// Hangfire: SQLite-backed so job history/dashboard state survives restarts.
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSQLiteStorage(hangfireDbPath));
builder.Services.AddHangfireServer();

// Rate limiting + output caching for the public /digest endpoint — it only
// serves pre-generated, non-sensitive content, so this (plus strict category
// whitelisting below) is proportionate; no API-key auth added.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("digest", limiterOptions =>
    {
        limiterOptions.PermitLimit = 30;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });

    // Separate limiters rather than sharing "digest": AddFixedWindowLimiter
    // creates a SINGLE GLOBAL window, not one partitioned per caller, so
    // attaching a second consumer to it would eat the public digest endpoint's
    // budget.
    options.AddFixedWindowLimiter("digest-categories", limiterOptions =>
    {
        limiterOptions.PermitLimit = 60;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });

    // Generation requests are cheap to serve but represent expensive work, so
    // they are limited well below the read endpoints even though the coordinator
    // already refuses to start redundant runs.
    options.AddFixedWindowLimiter("digest-generate", limiterOptions =>
    {
        limiterOptions.PermitLimit = 6;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });

    // Deliberately tight: this bounds both taxonomy spam and API-key guessing.
    options.AddFixedWindowLimiter("digest-admin", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
});

builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("digest", policy => policy
        .Expire(TimeSpan.FromHours(1))
        .SetVaryByQuery("categories"));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseOutputCache();

// Dashboard is locked down to local requests only — it lets anyone view/trigger/
// delete jobs, so it must never be left open on a public deployment.
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new LocalRequestsOnlyAuthorizationFilter()]
});

var digestOptions = app.Services.GetRequiredService<IOptions<DigestOptions>>().Value;

// Seeded explicitly at startup rather than from the provider's constructor: the
// singleton is resolved lazily, so bootstrapping the taxonomy off first-use
// would make the ordering implicit and hard to find. This also guarantees it
// runs before the first request and before Hangfire can fire the nightly job.
var categoryProvider = app.Services.GetRequiredService<DigestCategoryProvider>();
await categoryProvider.EnsureSeededAsync();

var categorySnapshot = categoryProvider.GetSnapshot();
app.Logger.LogInformation("Digest categories resolved ({Count}): {Categories}",
    categorySnapshot.EnabledNames.Count, string.Join(" | ", categorySnapshot.EnabledNames));

// Surfaced at startup so a missing key is diagnosable here rather than looking
// like a wrong key when an admin call comes back 503.
var adminOptions = app.Services.GetRequiredService<IOptions<AdminOptions>>().Value;
if (string.IsNullOrWhiteSpace(adminOptions.ApiKey))
{
    app.Logger.LogWarning(
        "Admin:ApiKey is not configured — the digest category admin endpoints will return 503. " +
        "Set it via user secrets or the Admin__ApiKey environment variable.");
}
else if (adminOptions.ApiKey.Length < AdminOptions.MinimumApiKeyLength)
{
    app.Logger.LogWarning(
        "Admin:ApiKey is shorter than the recommended {Minimum} characters; it is guessable",
        AdminOptions.MinimumApiKeyLength);
}

RecurringJob.AddOrUpdate<DigestJob>(
    "daily-digest",
    job => job.RunAsync(CancellationToken.None),
    digestOptions.CronSchedule);

// Dev-only manual trigger — the recurring job is nightly, so there'd otherwise
// be no way to exercise the pipeline on demand. Runs inline and returns the
// digest so failures surface in the response instead of only in job history.
if (app.Environment.IsDevelopment())
{
    app.MapPost("/digest/run", async (DigestJob job, DigestStorageService storage, CancellationToken ct) =>
    {
        await job.RunAsync(ct);
        return Results.Ok(OperationResponse<DigestDto>.Ok((await storage.GetLatestAsync())!));
    });
}

app.MapGet("/digest", async (string? categories, DigestStorageService storage, DigestCategoryProvider categoryProvider) =>
{
    var latest = await storage.GetLatestAsync();
    if (latest is null)
    {
        // Before the first nightly run there is genuinely nothing to serve.
        // Reporting that as Ok(null) makes every client null-check a "success"
        // payload; 404 + Fail says what actually happened.
        return Results.NotFound(OperationResponse<DigestDto>.Fail("No digest has been generated yet."));
    }

    if (string.IsNullOrWhiteSpace(categories))
    {
        return Results.Ok(OperationResponse<DigestDto>.Ok(latest));
    }

    // Whitelist: only known category names pass through; anything else is
    // silently dropped rather than causing an error or being reflected back.
    // Read from the injected provider, not a snapshot captured at startup, so an
    // admin change is honoured without a restart. Uses KnownNames rather than
    // EnabledNames so a stored digest referencing a since-disabled category can
    // still be filtered for.
    var known = categoryProvider.GetSnapshot().KnownNames;
    var requested = categories
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(c => known.Contains(c))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var filtered = latest with
    {
        Categories = latest.Categories.Where(c => requested.Contains(c.Name))
    };

    return Results.Ok(OperationResponse<DigestDto>.Ok(filtered));
})
.WithName("GetDigest")
.RequireRateLimiting("digest")
.CacheOutput("digest");

// ── Digest taxonomy ────────────────────────────────────────────────────────
//
// The public read is anonymous like /digest; everything that mutates the
// taxonomy sits behind AdminApiKeyEndpointFilter. Note that the array ORDER is
// the contract on both listings — the stored sort order is deliberately kept off
// the wire so no client re-derives (or disagrees about) the ordering.

// Lets the app ask for today's digest when it notices the stored one is from a
// previous day. Anonymous like the rest of the read path, which is safe because
// the coordinator refuses to start a run when today's digest already exists or
// one is already running — so this cannot cost more than the nightly job.
app.MapPost("/digest/generate", async (DigestGenerationCoordinator coordinator) =>
{
    var outcome = await coordinator.EnsureTodaysDigestAsync();
    var latest = await app.Services.GetRequiredService<DigestStorageService>().GetLatestAsync();

    var status = new DigestGenerationStatusDto(outcome.ToString(), latest?.GeneratedAtUtc);

    // 202 for "work is happening, come back later"; 200 when there was nothing
    // to do. The distinction is what tells the client whether to start polling.
    return outcome == DigestGenerationOutcome.AlreadyCurrent
        ? Results.Ok(OperationResponse<DigestGenerationStatusDto>.Ok(status))
        : Results.Accepted(value: OperationResponse<DigestGenerationStatusDto>.Ok(status));
})
.WithName("EnsureTodaysDigest")
.RequireRateLimiting("digest-generate");

app.MapGet("/digest/categories", (DigestCategoryProvider categoryProvider) =>
{
    var categories = categoryProvider.GetSnapshot().EnabledNames
        .Select(name => new DigestTaxonomyCategoryDto(name))
        .ToList();

    // An empty list is a valid answer, not a 404: unlike /digest, where "nothing
    // generated yet" is a genuinely different state, "no categories" is just an
    // empty taxonomy. Startup seeding means it should never actually be empty.
    return Results.Ok(OperationResponse<IReadOnlyList<DigestTaxonomyCategoryDto>>.Ok(categories));
})
.WithName("GetDigestCategories")
.RequireRateLimiting("digest-categories");
// Deliberately NOT output-cached: this serves an in-memory snapshot, so a cache
// would buy nothing and would only add a window where an admin change is
// invisible.

app.MapGet("/digest/categories/all", (DigestCategoryProvider categoryProvider) =>
{
    var categories = categoryProvider.GetSnapshot().All
        .Select(c => new DigestTaxonomyCategoryDto(c.Name, c.IsEnabled))
        .ToList();

    return Results.Ok(OperationResponse<IReadOnlyList<DigestTaxonomyCategoryDto>>.Ok(categories));
})
.WithName("GetAllDigestCategories")
.AddEndpointFilter<AdminApiKeyEndpointFilter>()
.RequireRateLimiting("digest-admin");

app.MapPost("/digest/categories", async (
        AddDigestCategoryRequest? request,
        DigestCategoryProvider categoryProvider) =>
{
    var result = await categoryProvider.AddAsync(request?.Name);
    return result.Outcome switch
    {
        // Location points at the collection: there is no per-category GET route,
        // and inventing one in a header would be a lie.
        CategoryWriteOutcome.Ok => Results.Created(
            "/digest/categories",
            OperationResponse<DigestTaxonomyCategoryDto>.Ok(
                new DigestTaxonomyCategoryDto(result.Category!.Name, result.Category.IsEnabled))),

        // 409, not 400: the input is well-formed, it is the *state* that conflicts.
        CategoryWriteOutcome.Duplicate => Conflict(result.Error!),
        _ => Results.BadRequest(OperationResponse<DigestTaxonomyCategoryDto>.Fail(result.Error!))
    };
})
.WithName("AddDigestCategory")
.AddEndpointFilter<AdminApiKeyEndpointFilter>()
.RequireRateLimiting("digest-admin");

app.MapPatch("/digest/categories/{name}", async (
        string name,
        SetDigestCategoryEnabledRequest? request,
        DigestCategoryProvider categoryProvider) =>
{
    if (request is null)
    {
        return Results.BadRequest(
            OperationResponse<DigestTaxonomyCategoryDto>.Fail("An isEnabled value is required."));
    }

    var result = await categoryProvider.SetEnabledAsync(name, request.IsEnabled);
    return result.Outcome switch
    {
        CategoryWriteOutcome.Ok => Results.Ok(
            OperationResponse<DigestTaxonomyCategoryDto>.Ok(
                new DigestTaxonomyCategoryDto(result.Category!.Name, result.Category.IsEnabled))),
        CategoryWriteOutcome.NotFound => Results.NotFound(
            OperationResponse<DigestTaxonomyCategoryDto>.Fail(result.Error!)),
        CategoryWriteOutcome.WouldEmptyTaxonomy => Conflict(result.Error!),
        _ => Results.BadRequest(OperationResponse<DigestTaxonomyCategoryDto>.Fail(result.Error!))
    };
})
.WithName("SetDigestCategoryEnabled")
.AddEndpointFilter<AdminApiKeyEndpointFilter>()
.RequireRateLimiting("digest-admin");

app.MapDelete("/digest/categories/{name}", async (
        string name,
        DigestCategoryProvider categoryProvider) =>
{
    var result = await categoryProvider.DeleteAsync(name);
    return result.Outcome switch
    {
        CategoryWriteOutcome.Ok => Results.Ok(
            OperationResponse<DigestTaxonomyCategoryDto>.Ok(
                new DigestTaxonomyCategoryDto(result.Category!.Name, result.Category.IsEnabled))),
        CategoryWriteOutcome.NotFound => Results.NotFound(
            OperationResponse<DigestTaxonomyCategoryDto>.Fail(result.Error!)),
        CategoryWriteOutcome.WouldEmptyTaxonomy => Conflict(result.Error!),
        _ => Results.BadRequest(OperationResponse<DigestTaxonomyCategoryDto>.Fail(result.Error!))
    };
})
.WithName("DeleteDigestCategory")
.AddEndpointFilter<AdminApiKeyEndpointFilter>()
.RequireRateLimiting("digest-admin");

app.MapPut("/digest/categories/order", async (
        ReorderDigestCategoriesRequest? request,
        DigestCategoryProvider categoryProvider) =>
{
    var result = await categoryProvider.ReorderAsync(request?.Names);
    return result.Outcome == CategoryWriteOutcome.Ok
        ? Results.Ok(OperationResponse<IReadOnlyList<DigestTaxonomyCategoryDto>>.Ok(
            categoryProvider.GetSnapshot().All
                .Select(c => new DigestTaxonomyCategoryDto(c.Name, c.IsEnabled))
                .ToList()))
        : Results.BadRequest(
            OperationResponse<IReadOnlyList<DigestTaxonomyCategoryDto>>.Fail(result.Error!));
})
.WithName("ReorderDigestCategories")
.AddEndpointFilter<AdminApiKeyEndpointFilter>()
.RequireRateLimiting("digest-admin");

app.Run();

static IResult Conflict(string error) =>
    Results.Json(OperationResponse<DigestTaxonomyCategoryDto>.Fail(error),
        statusCode: StatusCodes.Status409Conflict);
