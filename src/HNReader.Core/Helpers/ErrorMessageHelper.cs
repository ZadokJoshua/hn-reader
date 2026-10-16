using System.Net;
using System.Text.Json;
using HNReader.Core.Services;
using LiteDB;

namespace HNReader.Core.Helpers;

public enum UserErrorKind
{
    Offline,
    Timeout,
    ServerBusy,
    NotAvailable,
    BadData,
    FileInUse,
    FileMissing,
    NoPermission,
    DiskFull,
    FileProblem,
    Storage,
    NotFavouritesFile,
    EmptyFile,
    NothingToImport,
    FileTooLarge,
    Cancelled,
    Unknown
}

/// <summary>
/// What the user is told about a failure. <see cref="Message"/> is plain language and never
/// contains exception text, type names or paths; the full exception goes to the log instead.
/// </summary>
public sealed record UserFacingError(UserErrorKind Kind, string Message)
{
    /// <summary>True when trying again later can plausibly succeed (as opposed to fixing a file).</summary>
    public bool IsRetryable => Kind is UserErrorKind.Offline or UserErrorKind.Timeout
        or UserErrorKind.ServerBusy or UserErrorKind.Unknown or UserErrorKind.BadData;

    public bool IsOffline => Kind == UserErrorKind.Offline;
}

/// <summary>
/// Single place that turns exceptions into user-facing text, so UI code never shows
/// <see cref="Exception.Message"/>. Detailed text for logs is kept separate.
/// </summary>
public static class ErrorMessageHelper
{
    public static UserFacingError Describe(Exception ex)
    {
        // Task.WhenAll and friends wrap the real failure.
        if (ex is AggregateException agg && agg.InnerExceptions.Count == 1)
        {
            ex = agg.InnerExceptions[0];
        }

        switch (ex)
        {
            case FavoritesImportException import:
                return Make(import.Reason switch
                {
                    FavoritesImportFailure.EmptyFile => UserErrorKind.EmptyFile,
                    FavoritesImportFailure.NothingValid => UserErrorKind.NothingToImport,
                    FavoritesImportFailure.FileTooLarge => UserErrorKind.FileTooLarge,
                    _ => UserErrorKind.NotFavouritesFile
                });

            // HttpClient timeouts surface as TaskCanceledException wrapping a TimeoutException,
            // which must not be reported as the user cancelling.
            case OperationCanceledException when ex.InnerException is TimeoutException:
            case TimeoutException:
                return Make(UserErrorKind.Timeout);

            case OperationCanceledException:
                return Make(UserErrorKind.Cancelled);

            case HttpRequestException http:
                return http.StatusCode switch
                {
                    null => Make(UserErrorKind.Offline),
                    HttpStatusCode.TooManyRequests => Make(UserErrorKind.ServerBusy),
                    >= HttpStatusCode.InternalServerError => Make(UserErrorKind.ServerBusy),
                    _ => Make(UserErrorKind.NotAvailable)
                };

            case JsonException:
                return Make(UserErrorKind.BadData);

            case FileNotFoundException:
            case DirectoryNotFoundException:
                return Make(UserErrorKind.FileMissing);

            case UnauthorizedAccessException:
                return Make(UserErrorKind.NoPermission);

            case IOException io:
                // Win32 error code lives in the low 16 bits of HResult.
                return (io.HResult & 0xFFFF) switch
                {
                    32 or 33 => Make(UserErrorKind.FileInUse),
                    39 or 112 => Make(UserErrorKind.DiskFull),
                    _ => Make(UserErrorKind.FileProblem)
                };

            case LiteException:
                return Make(UserErrorKind.Storage);

            default:
                return Make(UserErrorKind.Unknown);
        }
    }

    /// <summary>Plain-language message for the UI.</summary>
    public static string GetUserMessage(Exception ex) => Describe(ex).Message;

    public static UserFacingError Make(UserErrorKind kind) => new(kind, kind switch
    {
        UserErrorKind.Offline => "You appear to be offline. Check your internet connection and try again.",
        UserErrorKind.Timeout => "Hacker News is taking too long to respond. Try again in a moment.",
        UserErrorKind.ServerBusy => "Hacker News is busy right now. Please try again shortly.",
        UserErrorKind.NotAvailable => "That content isn't available right now.",
        UserErrorKind.BadData => "We couldn't read the data we got back. Please try again.",
        UserErrorKind.FileInUse => "That file is open in another program. Close it and try again.",
        UserErrorKind.FileMissing => "We couldn't find that file or folder. It may have been moved or deleted.",
        UserErrorKind.NoPermission => "HN Reader doesn't have permission to use that location. Choose a different folder.",
        UserErrorKind.DiskFull => "There isn't enough free space to save the file.",
        UserErrorKind.FileProblem => "We couldn't read or write that file. Try a different location.",
        UserErrorKind.Storage => "Your favourites couldn't be accessed. Restart HN Reader and try again.",
        UserErrorKind.NotFavouritesFile => "That file isn't an HN Reader favourites export. Choose a .json file exported from this app.",
        UserErrorKind.EmptyFile => "That file is empty.",
        UserErrorKind.NothingToImport => "No favourites were found in that file.",
        UserErrorKind.FileTooLarge => "That file is too large to be a favourites export.",
        UserErrorKind.Cancelled => "The request was cancelled.",
        _ => "Something went wrong. Please try again."
    });

    /// <summary>Classifies the exception for logging, without user-facing wording.</summary>
    public static string ClassifyException(Exception ex) => Describe(ex).Kind.ToString();

    /// <summary>Detailed text for logs: type, message and inner message.</summary>
    public static string GetDetailedMessage(Exception ex, string? context = null)
    {
        var message = $"[{ClassifyException(ex)}] {ex.GetType().Name}: {ex.Message}";

        if (!string.IsNullOrEmpty(context))
        {
            message = $"{context} -> {message}";
        }

        if (ex.InnerException != null)
        {
            message += $" (Inner: {ex.InnerException.Message})";
        }

        return message;
    }
}
