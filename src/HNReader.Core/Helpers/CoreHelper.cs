using System.Text.Json;

namespace HNReader.Core.Helpers;

public static class CoreHelper
{
    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json);
}
