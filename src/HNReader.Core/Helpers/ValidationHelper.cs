using System.Diagnostics.CodeAnalysis;

namespace HNReader.Core.Helpers;

/// <summary>
/// Helper methods for input validation in public API surfaces.
/// Throws appropriate exceptions with clear parameter names for debugging.
/// </summary>
public static class ValidationHelper
{
    /// <summary>
    /// Validates that a reference is not null.
    /// </summary>
    /// <param name="value">The value to check</param>
    /// <param name="paramName">The parameter name (for error message)</param>
    /// <exception cref="ArgumentNullException">Thrown if value is null</exception>
    public static void ThrowIfNull(
        [NotNull] object? value,
        string paramName)
    {
        if (value == null)
        {
            throw new ArgumentNullException(paramName, $"Parameter '{paramName}' cannot be null.");
        }
    }

    /// <summary>
    /// Validates that a string is not null or empty.
    /// </summary>
    /// <param name="value">The string to check</param>
    /// <param name="paramName">The parameter name (for error message)</param>
    /// <exception cref="ArgumentException">Thrown if string is null or empty</exception>
    public static void ThrowIfNullOrEmpty(
        [NotNull] string? value,
        string paramName)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException($"Parameter '{paramName}' cannot be null or empty.", paramName);
        }
    }

    /// <summary>
    /// Validates that a string is not null, empty, or only whitespace.
    /// </summary>
    /// <param name="value">The string to check</param>
    /// <param name="paramName">The parameter name (for error message)</param>
    /// <exception cref="ArgumentException">Thrown if string is null, empty, or whitespace</exception>
    public static void ThrowIfNullOrWhiteSpace(
        [NotNull] string? value,
        string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Parameter '{paramName}' cannot be null, empty, or whitespace.", paramName);
        }
    }

    /// <summary>
    /// Validates that an integer is within a specified range.
    /// </summary>
    /// <param name="value">The value to check</param>
    /// <param name="minInclusive">Minimum allowed value (inclusive)</param>
    /// <param name="maxInclusive">Maximum allowed value (inclusive)</param>
    /// <param name="paramName">The parameter name (for error message)</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if value is outside range</exception>
    public static void ThrowIfOutOfRange(
        int value,
        int minInclusive,
        int maxInclusive,
        string paramName)
    {
        if (value < minInclusive || value > maxInclusive)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                $"Parameter '{paramName}' must be between {minInclusive} and {maxInclusive}, but was {value}.");
        }
    }

    /// <summary>
    /// Validates that a collection contains at least one item.
    /// </summary>
    /// <typeparam name="T">The collection element type</typeparam>
    /// <param name="collection">The collection to check</param>
    /// <param name="paramName">The parameter name (for error message)</param>
    /// <exception cref="ArgumentException">Thrown if collection is null or empty</exception>
    public static void ThrowIfEmpty<T>(
        [NotNull] ICollection<T>? collection,
        string paramName)
    {
        if (collection == null || collection.Count == 0)
        {
            throw new ArgumentException($"Parameter '{paramName}' cannot be null or empty.", paramName);
        }
    }

    /// <summary>
    /// Validates that a URL is well-formed and uses a safe scheme.
    /// </summary>
    /// <param name="url">The URL to validate</param>
    /// <param name="paramName">The parameter name (for error message)</param>
    /// <exception cref="ArgumentException">Thrown if URL is invalid or uses unsafe scheme</exception>
    public static void ThrowIfInvalidUrl(string url, string paramName)
    {
        ThrowIfNullOrWhiteSpace(url, paramName);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException($"Parameter '{paramName}' is not a valid URL: {url}", paramName);
        }

        // Only allow http and https schemes
        if (uri.Scheme != "http" && uri.Scheme != "https")
        {
            throw new ArgumentException(
                $"Parameter '{paramName}' uses an unsafe scheme '{uri.Scheme}'. Only http and https are allowed.",
                paramName);
        }
    }

    /// <summary>
    /// Validates that a path is within an allowed directory (prevents path traversal).
    /// </summary>
    /// <param name="basePath">The allowed base directory</param>
    /// <param name="fullPath">The full path to validate</param>
    /// <param name="paramName">The parameter name (for error message)</param>
    /// <exception cref="ArgumentException">Thrown if path is outside the base directory</exception>
    public static void ThrowIfPathTraversal(string basePath, string fullPath, string paramName)
    {
        ThrowIfNullOrWhiteSpace(basePath, nameof(basePath));
        ThrowIfNullOrWhiteSpace(fullPath, nameof(fullPath));

        var resolvedBase = Path.GetFullPath(basePath);
        var resolvedFull = Path.GetFullPath(fullPath);

        if (!resolvedFull.StartsWith(resolvedBase, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Parameter '{paramName}' attempts path traversal outside allowed directory.",
                paramName);
        }
    }
}
