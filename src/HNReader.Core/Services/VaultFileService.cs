using HNReader.Core.Constants;
using HNReader.Core.Enums;
using HNReader.Core.Helpers;
using HNReader.Core.Interfaces;
using HNReader.Core.Models;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace HNReader.Core.Services;

/// <summary>
/// Manages file and folder operations within the knowledge base vault.
/// All relative paths are resolved against {VaultPath}/hn_knowledge_base/.
/// </summary>
public class VaultFileService : IVaultFileService
{
    private static readonly IReadOnlyDictionary<string, string> TemplateTokenReplacements =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { AppTemplateTokens.NEWS_DIGEST_FILE_NAME, AppFileNames.DIGEST_FILE_NAME },
            { AppTemplateTokens.UNPROCESSED_DIGEST_DATA_FILE_NAME, AppFileNames.UNPROCESSED_DIGEST_DATA_FILE_NAME },
            { AppTemplateTokens.NEWS_DIGEST_FOLDER, AppFolderNames.NEWS_DIGEST_FOLDER },
            { AppTemplateTokens.STORIES_FOLDER, AppFolderNames.STORIES_FOLDER },
            { AppTemplateTokens.STORIES_INDEX_FILE_NAME, AppFileNames.STORIES_INDEX_FILE_NAME },
            { AppTemplateTokens.STORY_FILE_TEMPLATE, AppFileTemplates.STORY_FILE_TEMPLATE },
            { AppTemplateTokens.STORY_SUMMARY_TEMPLATE, AppFileTemplates.STORY_SUMMARY_TEMPLATE }
        };

    private string? _basePath;

    public string? BasePath => _basePath;

    public VaultFileService(string? initialVaultPath)
    {
        if (!string.IsNullOrEmpty(initialVaultPath))
        {
            _basePath = Path.Combine(initialVaultPath, AppFolderNames.KNOWLEDGE_BASE_FOLDER);
        }
    }

    public void SetBasePath(string? vaultPath)
    {
        _basePath = string.IsNullOrEmpty(vaultPath)
            ? null
            : Path.Combine(vaultPath, AppFolderNames.KNOWLEDGE_BASE_FOLDER);
    }

    public Task<bool> FileExistsAsync(string relativePath)
    {
        EnsureBasePathSet();
        var fullPath = GetFullPath(relativePath);
        return Task.FromResult(File.Exists(fullPath));
    }

    public Task<bool> FolderExistsAsync(string relativePath)
    {
        EnsureBasePathSet();
        var fullPath = GetFullPath(relativePath);
        return Task.FromResult(Directory.Exists(fullPath));
    }

    public Task CreateFolderAsync(string relativePath)
    {
        EnsureBasePathSet();
        var fullPath = GetFullPath(relativePath);

        try
        {
            Directory.CreateDirectory(fullPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error creating folder '{relativePath}': {ex.Message}");
            throw;
        }

        return Task.CompletedTask;
    }

    public async Task CreateFileAsync(string relativePath, string content)
    {
        EnsureBasePathSet();
        var fullPath = GetFullPath(relativePath);

        try
        {
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(fullPath, content);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error creating file '{relativePath}': {ex.Message}");
            throw;
        }
    }

    public async Task<string> ReadTextAsync(string relativePath)
    {
        EnsureBasePathSet();
        var fullPath = GetFullPath(relativePath);

        try
        {
            return await File.ReadAllTextAsync(fullPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error reading file '{relativePath}': {ex.Message}");
            throw;
        }
    }

    public async Task WriteTextAsync(string relativePath, string content)
    {
        EnsureBasePathSet();
        var fullPath = GetFullPath(relativePath);

        try
        {
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(fullPath, content);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error writing file '{relativePath}': {ex.Message}");
            throw;
        }
    }

    public Task DeleteFileAsync(string relativePath)
    {
        EnsureBasePathSet();
        var fullPath = GetFullPath(relativePath);

        try
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error deleting file '{relativePath}': {ex.Message}");
            throw;
        }

        return Task.CompletedTask;
    }

    public Task DeleteFolderAsync(string relativePath, bool recursive = false)
    {
        EnsureBasePathSet();
        var fullPath = GetFullPath(relativePath);

        try
        {
            if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, recursive);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error deleting folder '{relativePath}': {ex.Message}");
            throw;
        }

        return Task.CompletedTask;
    }

    public Task<IEnumerable<string>> GetFilesAsync(string relativePath, string searchPattern = "*")
    {
        EnsureBasePathSet();
        var fullPath = GetFullPath(relativePath);

        try
        {
            if (!Directory.Exists(fullPath))
            {
                return Task.FromResult(Enumerable.Empty<string>());
            }

            var files = Directory.GetFiles(fullPath, searchPattern)
                .Select(f => Path.GetRelativePath(_basePath!, f));

            return Task.FromResult(files);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error listing files in '{relativePath}': {ex.Message}");
            throw;
        }
    }

    public async Task InitializeKnowledgeBaseAsync()
    {
        EnsureBasePathSet();

        // Create the root knowledge base directory
        Directory.CreateDirectory(_basePath!);

        // Create subfolders for each knowledge base category
        foreach (var folder in Enum.GetValues<KnowledgeBaseFolders>())
        {
            var folderName = folder.GetDescription();
            Directory.CreateDirectory(Path.Combine(_basePath!, folderName));
        }

        // Deploy Agent.md files from embedded resources
        await DeployEmbeddedResourceIfMissingAsync(AppEmbeddedResources.AGENT, AppFileNames.AGENT_INSTRUCTIONS_FILE_NAME);
        await   DeployEmbeddedResourceIfMissingAsync(AppEmbeddedResources.NEWS_DIGEST_AGENT,
            Path.Combine(KnowledgeBaseFolders.NewsDigest.GetDescription(), AppFileNames.AGENT_INSTRUCTIONS_FILE_NAME));
        await DeployEmbeddedResourceIfMissingAsync(AppEmbeddedResources.STORIES_AGENT,
            Path.Combine(KnowledgeBaseFolders.Stories.GetDescription(), AppFileNames.AGENT_INSTRUCTIONS_FILE_NAME));
    }

    public Task CleanupKnowledgeBaseAsync()
    {
        if (string.IsNullOrEmpty(_basePath))
        {
            return Task.CompletedTask;
        }

        try
        {
            if (Directory.Exists(_basePath))
            {
                Directory.Delete(_basePath, recursive: true);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error cleaning up knowledge base: {ex.Message}");
            throw;
        }

        return Task.CompletedTask;
    }

    public async Task<DigestOutputDto?> LoadDigestAsync()
    {
        EnsureBasePathSet();

        var digestPath = Path.Combine(
            KnowledgeBaseFolders.NewsDigest.GetDescription(), AppFileNames.DIGEST_FILE_NAME);
        var fullPath = GetFullPath(digestPath);

        if (!File.Exists(fullPath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(fullPath);
            return JsonSerializer.Deserialize<DigestOutputDto>(json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading digest JSON: {ex.Message}");
            return null;
        }
    }

    public async Task WriteDigestJsonAsync(string digestJson, CancellationToken cancellationToken = default)
    {
        EnsureBasePathSet();

        if (string.IsNullOrWhiteSpace(digestJson))
        {
            throw new ArgumentException("Digest JSON is empty.", nameof(digestJson));
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<DigestOutputDto>(digestJson) ?? throw new InvalidDataException("Digest JSON is invalid or empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Digest JSON is invalid.", ex);
        }

        var digestPath = Path.Combine(
            KnowledgeBaseFolders.NewsDigest.GetDescription(), AppFileNames.DIGEST_FILE_NAME);
        var fullPath = GetFullPath(digestPath);

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        await File.WriteAllTextAsync(fullPath, digestJson, cancellationToken);
    }

    public async Task SaveDigestAsync(DigestOutputDto digest)
    {
        EnsureBasePathSet();

        var json = JsonSerializer.Serialize(digest, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        var digestPath = Path.Combine(
            KnowledgeBaseFolders.NewsDigest.GetDescription(), AppFileNames.DIGEST_FILE_NAME);
        var fullPath = GetFullPath(digestPath);

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(fullPath, json);
        Debug.WriteLine($"[VaultFileService] Saved enriched digest to {fullPath}");
    }

    public Task SaveStoryMarkdownAsync(int storyId, string markdownContent)
    {
        EnsureBasePathSet();

        if (storyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(storyId), "Story ID must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(markdownContent))
        {
            throw new ArgumentException("Markdown content cannot be empty.", nameof(markdownContent));
        }

        var relativePath = GetStoryMarkdownRelativePath(storyId);
        return WriteTextAsync(relativePath, markdownContent);
    }

    public Task DeleteStoryByIdAsync(int storyId)
    {
        EnsureBasePathSet();

        if (storyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(storyId), "Story ID must be greater than zero.");
        }

        var relativePath = GetStoryMarkdownRelativePath(storyId);
        return DeleteFileAsync(relativePath);
    }

    /// <summary>
    /// Resolves a relative path against the knowledge base root and validates
    /// the result stays within bounds (prevents path traversal).
    /// </summary>
    internal string GetFullPath(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        var fullPath = Path.GetFullPath(Path.Combine(_basePath!, relativePath));

        if (!fullPath.StartsWith(_basePath!, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException(
                $"Access denied: path '{relativePath}' resolves outside the knowledge base.");
        }

        return fullPath;
    }

    private void EnsureBasePathSet()
    {
        if (string.IsNullOrEmpty(_basePath))
        {
            throw new InvalidOperationException(
                "Vault path is not set. Select a vault folder before performing file operations.");
        }
    }

    private async Task DeployEmbeddedResourceIfMissingAsync(string resourceName, string relativePath)
    {
        EnsureBasePathSet();

        var fullPath = GetFullPath(relativePath);
        if (File.Exists(fullPath)) return;

        await DeployEmbeddedResourceAsync(resourceName, relativePath);
    }

    private async Task DeployEmbeddedResourceAsync(string resourceName, string relativePath)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);

        if (stream == null)
        {
            Debug.WriteLine($"Embedded resource '{resourceName}' not found.");
            return;
        }

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        var resolvedContent = ReplaceTemplateTokens(content);
        await CreateFileAsync(relativePath, resolvedContent);
    }

    private static string ReplaceTemplateTokens(string content)
    {
        if (string.IsNullOrEmpty(content)) return content;

        var updatedContent = content;
        foreach (var replacement in TemplateTokenReplacements)
        {
            updatedContent = updatedContent.Replace(replacement.Key, replacement.Value, StringComparison.Ordinal);
        }

        return updatedContent;
    }

    private static string GetStoryMarkdownRelativePath(int storyId)
    {
        return Path.Combine(AppFolderNames.STORIES_FOLDER, $"{storyId}.md");
    }
}
