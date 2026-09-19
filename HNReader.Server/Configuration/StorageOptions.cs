namespace HNReader.Server.Configuration;

/// <summary>
/// Bound from the "Storage" configuration section. Backs both the Hangfire
/// SQLite store and the digest LiteDB file — a relative path locally, or the
/// Dockerfile's implied "/data" volume in a container.
/// </summary>
public class StorageOptions
{
    public string DataDirectory { get; set; } = "data";
}
