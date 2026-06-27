namespace HNReader.Core.Helpers;

/// <summary>
/// Helper class for classifying exceptions and generating user-friendly error messages.
/// Separates UI-facing messages (friendly) from detailed logging (verbose).
/// </summary>
public static class ErrorMessageHelper
{
    /// <summary>
    /// Gets a user-friendly error message for display in the UI.
    /// These messages are generic and don't expose implementation details.
    /// </summary>
    /// <param name="ex">The exception to classify</param>
    /// <returns>A user-friendly error message</returns>
    public static string GetUserMessage(Exception ex)
    {
        return ex switch
        {
            HttpRequestException => "Network error. Please check your internet connection.",

            OperationCanceledException => "The request was cancelled. Please try again.",

            TimeoutException => "Request timed out. Please try again.",

            IOException => "File operation failed. Please check file permissions and try again.",

            UnauthorizedAccessException => "Permission denied. Please check file permissions.",

            ArgumentException exArg => $"Invalid input: {exArg.ParamName}. Please check your settings.",

            _ => "An unexpected error occurred. Please try again later."
        };
    }

    /// <summary>
    /// Classifies the exception type for logging and telemetry.
    /// Useful for aggregating error stats without exposing user details.
    /// </summary>
    /// <param name="ex">The exception to classify</param>
    /// <returns>A classification string (e.g., "NetworkError", "FileIOError")</returns>
    public static string ClassifyException(Exception ex)
    {
        return ex switch
        {
            HttpRequestException => "NetworkError",
            OperationCanceledException => "OperationCancelled",
            TimeoutException => "Timeout",
            InvalidOperationException => "InvalidOperation",
            IOException => "FileIOError",
            UnauthorizedAccessException => "PermissionDenied",
            ArgumentException => "InvalidArgument",
            _ => "UnknownError"
        };
    }

    /// <summary>
    /// Gets a detailed error message suitable for logging.
    /// Includes exception details, stack trace hints, and context.
    /// </summary>
    /// <param name="ex">The exception to detail</param>
    /// <param name="context">Optional context about where the error occurred</param>
    /// <returns>A detailed error message for logs</returns>
    public static string GetDetailedMessage(Exception ex, string? context = null)
    {
        var classification = ClassifyException(ex);
        var message = $"[{classification}] {ex.GetType().Name}: {ex.Message}";
        
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
