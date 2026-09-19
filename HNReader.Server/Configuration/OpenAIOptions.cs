namespace HNReader.Server.Configuration;

/// <summary>
/// Bound from the "OpenAI" configuration section. <see cref="ApiKey"/> must come
/// from user secrets or an environment variable (OpenAI__ApiKey) — never commit
/// it to appsettings.json.
/// </summary>
public class OpenAIOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gpt-4o-mini";
}
