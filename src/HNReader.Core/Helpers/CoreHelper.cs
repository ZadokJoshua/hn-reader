using System.Text.Json;

namespace HNReader.Core.Helpers;

public static class CoreHelper
{
    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json);

    public static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Options for reading JSON produced by our own ASP.NET server.
    /// <para>
    /// This exists because <see cref="Deserialize{T}"/> passes no options at all,
    /// and that combination is silently wrong for a web payload: ASP.NET
    /// serializes camelCase, System.Text.Json matches property names
    /// case-sensitively by default, and a positional record whose constructor
    /// parameters go unmatched is filled with defaults rather than throwing. The
    /// result is a fully-populated-looking object with a default timestamp and a
    /// null collection, and no exception anywhere to point at the cause.
    /// </para>
    /// <para>
    /// <see cref="JsonSerializerDefaults.Web"/> supplies camelCase naming,
    /// case-insensitive matching, and number-from-string reading in one go.
    /// </para>
    /// </summary>
    public static readonly JsonSerializerOptions WebJsonSerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Deserializes a payload from our server. See <see cref="WebJsonSerializerOptions"/>.</summary>
    public static T? DeserializeWeb<T>(string json) => JsonSerializer.Deserialize<T>(json, WebJsonSerializerOptions);
}
