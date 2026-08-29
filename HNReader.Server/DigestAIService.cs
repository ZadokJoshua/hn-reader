namespace HNReader.Server;

public class DigestAIService
{
    private readonly string _storagePath;
    private readonly string _dbPath;

    private readonly ILogger<DigestAIService> _logger;

    public DigestAIService(ILogger<DigestAIService> logger)
    {
        _storagePath = "//data";
        _dbPath = _storagePath + "//db";

        _logger = logger;
    }

    public async Task GenerateDailyDigestAAsync()
    {
        // Fetch data from Algolia API
        var fetchedData = await FetchDataFromAlgoliaAsync();

        // Store fetched data in a document for processing
        // Generate digest
        // Store digest in database
    }

    private async Task FetchDataFromAlgoliaAsync()
    {
        var twentyFourHoursAgoTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 86400; // 24 hours ago
        var totalHits = 50; // Number of stories to fetch

        var url = "https://hn.algolia.com/api/v1/search?tags=story&numericFilters=created_at_i%3E" + twentyFourHoursAgoTimestamp.ToString() + "&hitsPerPage=" + totalHits.ToString() + "&page=0";

    }
}
