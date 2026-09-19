using HNReader.Shared.Models;

namespace HNReader.Core.Models;

/// <summary>
/// Why a digest fetch ended the way it did.
/// <para>
/// <see cref="NotGenerated"/> and <see cref="RateLimited"/> are modelled as
/// outcomes rather than exceptions because neither is a fault: the server
/// documents a 404 as the normal state before the first nightly run, and a 429
/// is the rate limiter working. Keeping them out of the exception path leaves
/// the caller's catch block meaning "the network or the server actually failed".
/// </para>
/// </summary>
public enum DigestStatus
{
    Ok,
    NotGenerated,
    RateLimited
}

/// <param name="ServerMessage">
/// The server's own explanation, when it sent one. Preferred over a hardcoded
/// client string so the user sees what the server actually said.
/// </param>
public sealed record DigestResult(DigestStatus Status, DigestDto? Digest, string? ServerMessage)
{
    public static DigestResult Ok(DigestDto digest) => new(DigestStatus.Ok, digest, null);
    public static DigestResult NotGenerated(string? message) => new(DigestStatus.NotGenerated, null, message);
    public static DigestResult RateLimited() => new(DigestStatus.RateLimited, null, null);
}
