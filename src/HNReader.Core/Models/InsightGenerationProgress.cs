namespace HNReader.Core.Models;

/// <summary>
/// Reports progress from story insight generation to the UI.
/// Used with <see cref="IProgress{InsightGenerationProgress}"/>.
/// </summary>
public class InsightGenerationProgress
{
    public int Percentage { get; init; }

    public string Message { get; init; } = string.Empty;

    public bool IsComplete { get; init; }

    public bool HasError { get; init; }

    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The generated insight text when complete.
    /// </summary>
    public string? InsightText { get; init; }

    public static InsightGenerationProgress Stage(int percentage, string message) =>
        new() { Percentage = percentage, Message = message };

    public static InsightGenerationProgress Complete(string insightText) =>
        new() { Percentage = 100, Message = "Insight ready!", IsComplete = true, InsightText = insightText };

    public static InsightGenerationProgress Error(string errorMessage) =>
        new() { Percentage = 0, Message = errorMessage, HasError = true, ErrorMessage = errorMessage };
}
