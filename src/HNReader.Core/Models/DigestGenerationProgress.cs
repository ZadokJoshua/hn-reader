namespace HNReader.Core.Models;

/// <summary>
/// Reports progress from the digest generation pipeline to the UI.
/// Used with <see cref="IProgress{DigestGenerationProgress}"/>.
/// </summary>
public class DigestGenerationProgress
{
    public int Percentage { get; init; }

    public string Message { get; init; } = string.Empty;

    public bool IsComplete { get; init; }

    public bool HasError { get; init; }

    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The deserialized digest result, set when generation completes successfully.
    /// </summary>
    public DigestOutputDto? Result { get; init; }

    public static DigestGenerationProgress Stage(int percentage, string message) =>
        new() { Percentage = percentage, Message = message };

    public static DigestGenerationProgress Complete(DigestOutputDto? result) =>
        new() { Percentage = 100, Message = "Digest ready!", IsComplete = true, Result = result };

    public static DigestGenerationProgress Error(string errorMessage) =>
        new() { Percentage = 0, Message = errorMessage, HasError = true, ErrorMessage = errorMessage };
}
