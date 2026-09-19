using System.Security.Cryptography;
using System.Text;
using HNReader.Server.Configuration;
using HNReader.Shared.Models;
using Microsoft.Extensions.Options;

namespace HNReader.Server;

/// <summary>
/// Gates the super-admin taxonomy endpoints on a shared secret sent in the
/// <see cref="HeaderName"/> header.
///
/// <para>
/// An <see cref="IEndpointFilter"/> rather than middleware: a filter is attached
/// to the exact routes it guards, so it cannot drift out of sync with the route
/// table the way a path-matching middleware would. It's also why the public
/// /digest endpoints can't be caught by it accidentally.
/// </para>
///
/// <para>
/// A shared secret is proportionate here — one super admin, a handful of
/// endpoints, and a read path that is deliberately anonymous. It buys nothing to
/// model user identities, token expiry, or claims for this.
/// </para>
/// </summary>
public class AdminApiKeyEndpointFilter : IEndpointFilter
{
    /// <summary>
    /// Deliberately not <c>Authorization</c>: this is not a registered auth
    /// scheme, and naming it as one invites a client to assume Bearer semantics
    /// that don't exist here.
    /// </summary>
    public const string HeaderName = "X-Admin-Api-Key";

    private readonly AdminOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AdminApiKeyEndpointFilter> _logger;

    public AdminApiKeyEndpointFilter(
        IOptions<AdminOptions> options,
        IHostEnvironment environment,
        ILogger<AdminApiKeyEndpointFilter> logger)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;

        // Fail closed. A deployment that never configured a key must not expose
        // taxonomy mutation, and 503 says "this endpoint isn't usable here"
        // rather than sending an operator hunting for a typo in their key.
        // Mapping the route unconditionally (and rejecting here) beats skipping
        // MapPost, which would 404 in some deployments and vanish from OpenAPI.
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogError(
                "Admin endpoint {Path} refused: Admin:ApiKey is not configured", request.Path);
            return Fail(
                "This endpoint is unavailable because no admin API key is configured on the server.",
                StatusCodes.Status503ServiceUnavailable);
        }

        // The secret travels in a header, so it must not cross the wire in
        // plaintext. UseHttpsRedirection cannot save us here: by the time its
        // 307 arrives, the plaintext request carrying the key has already been
        // sent. Development is exempt because the http launch profile has no
        // HTTPS port at all.
        if (!request.IsHttps && !_environment.IsDevelopment())
        {
            _logger.LogWarning("Admin endpoint {Path} refused: request was not HTTPS", request.Path);
            return Fail("This endpoint requires HTTPS.", StatusCodes.Status403Forbidden);
        }

        var provided = request.Headers[HeaderName].ToString();

        // 401 for "you sent no credentials", 403 for "you sent the wrong ones" —
        // re-prompting is pointless in the second case, which is exactly the
        // distinction the two codes carry.
        if (string.IsNullOrEmpty(provided))
        {
            return Fail($"The {HeaderName} header is required.", StatusCodes.Status401Unauthorized);
        }

        if (!IsMatch(provided, _options.ApiKey))
        {
            _logger.LogWarning(
                "Admin endpoint {Path} refused: invalid API key from {RemoteIp}",
                request.Path, context.HttpContext.Connection.RemoteIpAddress);
            return Fail("The supplied admin API key is not valid.", StatusCodes.Status403Forbidden);
        }

        return await next(context);
    }

    /// <summary>
    /// Constant-time comparison. A plain <c>==</c> on a secret is a timing
    /// oracle, and avoiding it costs nothing. The length check first is safe to
    /// leak — key length is not the secret.
    /// </summary>
    private static bool IsMatch(string provided, string expected)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);

        return providedBytes.Length == expectedBytes.Length
               && CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }

    // Rejections use the same envelope as every successful response, so a client
    // never has to branch on two different error shapes.
    private static IResult Fail(string error, int statusCode) =>
        Results.Json(OperationResponse<object>.Fail(error), statusCode: statusCode);
}
