using System.Threading;
using System.Threading.Tasks;

namespace HNReader.Core.Interfaces;

/// <summary>
/// Fetches site icons, with caching and a provider fallback chain.
/// </summary>
public interface IFaviconService
{
    /// <summary>
    /// Raw icon bytes for <paramref name="faviconHost"/>, or <c>null</c> when no provider
    /// returned a usable image.
    /// </summary>
    /// <param name="faviconHost">
    /// A bare host such as <c>simonw.github.io</c> - normally from
    /// <see cref="Helpers.DomainHelper.GetFaviconHost"/>. This takes a host rather than a
    /// URL on purpose: it keeps callers from re-introducing the apex truncation that made
    /// icons resolve to the wrong site.
    /// </param>
    Task<byte[]?> GetFaviconAsync(string faviconHost, CancellationToken cancellationToken = default);
}
