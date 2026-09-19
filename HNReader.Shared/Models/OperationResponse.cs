namespace HNReader.Shared.Models;

/// <summary>
/// Envelope for every API response. Lives here rather than in the server so the
/// clients deserialize the exact type the server serializes — a hand-copied
/// duplicate is how the two drift apart silently.
/// </summary>
public class OperationResponse<T>
{
    public bool Success { get; set; } = true;
    public string? Error { get; set; }
    public T? Data { get; set; }

    public static OperationResponse<T> Ok(T data) => new() { Data = data };

    public static OperationResponse<T> Fail(string error) => new() { Success = false, Error = error };
}
