namespace HNReader.Core.Services;

public enum FavoritesImportFailure
{
    EmptyFile,
    NotFavouritesFile,
    NothingValid,
    FileTooLarge
}

/// <summary>The chosen file cannot be used as a favourites export. Carries a reason, not UI text.</summary>
public sealed class FavoritesImportException : Exception
{
    public FavoritesImportFailure Reason { get; }

    public FavoritesImportException(FavoritesImportFailure reason, Exception? inner = null)
        : base($"Favourites import failed: {reason}", inner)
    {
        Reason = reason;
    }
}

/// <summary>Outcome of a successful import. <see cref="Skipped"/> counts unusable entries.</summary>
public readonly record struct FavoritesImportResult(int Added, int Updated, int Skipped)
{
    public int Total => Added + Updated;
}
