namespace HNReader.Server.Configuration;

/// <summary>
/// Bound from the "Admin" configuration section. Guards the super-admin
/// taxonomy endpoints.
///
/// <para>
/// <see cref="ApiKey"/> is a secret and must <b>never</b> appear in the
/// committed <c>appsettings.json</c> — supply it the same way
/// <c>OpenAI:ApiKey</c> is supplied: <c>dotnet user-secrets set "Admin:ApiKey"
/// …</c> in development, or the <c>Admin__ApiKey</c> environment variable in a
/// deployment.
/// </para>
///
/// <para>
/// When it is unset the admin endpoints <b>fail closed</b> (503) rather than
/// being open. That is the whole point of putting it here instead of defaulting
/// to something: a default admin key is worse than no admin endpoint.
/// </para>
/// </summary>
public class AdminOptions
{
    /// <summary>
    /// Below this length a "key" is guessable enough to be misleading rather
    /// than protective, so startup warns about it. Not enforced — refusing to
    /// start over a short key would be a worse failure than warning.
    /// </summary>
    public const int MinimumApiKeyLength = 32;

    public string ApiKey { get; set; } = string.Empty;
}
