using HNReader.Core.Models;

namespace HNReader.Core.Interfaces;

/// <summary>
/// Provides file and folder management operations scoped to the knowledge base vault.
/// All paths are relative to the vault's hn_knowledge_base directory.
/// </summary>
public interface IVaultFileService
{
    /// <summary>
    /// Gets the current base path (hn_knowledge_base root) or null if no vault is set.
    /// </summary>
    string? BasePath { get; }

    /// <summary>
    /// Updates the vault base path. Sets BasePath to {vaultPath}/hn_knowledge_base.
    /// Pass null to clear the base path.
    /// </summary>
    void SetBasePath(string? vaultPath);

    /// <summary>
    /// Checks whether a file exists at the given path relative to the knowledge base root.
    /// </summary>
    Task<bool> FileExistsAsync(string relativePath);

    /// <summary>
    /// Checks whether a folder exists at the given path relative to the knowledge base root.
    /// </summary>
    Task<bool> FolderExistsAsync(string relativePath);

    /// <summary>
    /// Creates a folder (and any parent directories) at the given relative path.
    /// No-op if the folder already exists.
    /// </summary>
    Task CreateFolderAsync(string relativePath);

    /// <summary>
    /// Creates a file with the specified content. Overwrites if the file already exists.
    /// Parent directories are created automatically.
    /// </summary>
    Task CreateFileAsync(string relativePath, string content);

    /// <summary>
    /// Reads the full text content of a file at the given relative path.
    /// </summary>
    Task<string> ReadTextAsync(string relativePath);

    /// <summary>
    /// Writes text content to a file, replacing any existing content.
    /// Parent directories are created automatically.
    /// </summary>
    Task WriteTextAsync(string relativePath, string content);

    /// <summary>
    /// Deletes a file at the given relative path. No-op if the file does not exist.
    /// </summary>
    Task DeleteFileAsync(string relativePath);

    /// <summary>
    /// Deletes a folder at the given relative path.
    /// No-op if the folder does not exist.
    /// </summary>
    /// <param name="relativePath">Relative path to the folder.</param>
    /// <param name="recursive">If true, deletes the folder and all its contents.</param>
    Task DeleteFolderAsync(string relativePath, bool recursive = false);

    /// <summary>
    /// Returns file paths matching a search pattern within the given relative directory.
    /// Paths are returned relative to the knowledge base root.
    /// </summary>
    /// <param name="relativePath">Relative directory to search in.</param>
    /// <param name="searchPattern">File glob pattern (e.g. "*.md", "*.json").</param>
    Task<IEnumerable<string>> GetFilesAsync(string relativePath, string searchPattern = "*");

    /// <summary>
    /// Initializes the full knowledge base folder structure and deploys Agent.md
    /// instruction files from embedded resources.
    /// </summary>
    Task InitializeKnowledgeBaseAsync();

    /// <summary>
    /// Deletes the entire hn_knowledge_base directory and all its contents.
    /// </summary>
    Task CleanupKnowledgeBaseAsync();

    /// <summary>
    /// Reads and deserializes the single digest.json file from the news_digest folder.
    /// Returns null if the file does not exist or is invalid.
    /// </summary>
    Task<DigestOutputDto?> LoadDigestAsync();

    /// <summary>
    /// Clears the existing digest file (if any) and writes a new digest JSON payload.
    /// The JSON must deserialize to <see cref="DigestOutputDto"/>.
    /// </summary>
    Task WriteDigestJsonAsync(string digestJson, CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializes and saves an enriched <see cref="DigestOutputDto"/> back to the digest.json file.
    /// Used to persist image URLs and generation timestamps after agent completion.
    /// </summary>
    Task SaveDigestAsync(DigestOutputDto digest);

    /// <summary>
    /// Saves story markdown content as stories/{id}.md in the knowledge base.
    /// Overwrites any existing file for the same story ID.
    /// </summary>
    Task SaveStoryMarkdownAsync(int storyId, string markdownContent);

    /// <summary>
    /// Deletes stories/{id}.md from the knowledge base.
    /// No-op if the file does not exist.
    /// </summary>
    Task DeleteStoryByIdAsync(int storyId);
}
