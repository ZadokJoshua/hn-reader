using GitHub.Copilot.SDK;
using HNReader.Core.Constants;
using HNReader.Core.Models;
using System.Diagnostics;
using HNReader.Core.Interfaces;
using Microsoft.Extensions.AI;

namespace HNReader.Core.Services;

/// <summary>
/// Orchestrates digest generation: fetches unprocessed HN data, saves it to the knowledge base,
/// then hands off to a Copilot CLI session to produce digest.json.
/// </summary>
public class CopilotCliService(
    ISettingsService settingsService,
    CopilotFunctions copilotFunctions,
    IVaultFileService vaultFileService)
{
    private const string DefaultCopilotModel = "claude-sonnet-4.5";
    private static readonly HashSet<string> SupportedModels =
    [
        "claude-sonnet-4.5",
        "claude-opus-4-5",
        "gpt-5.2"
    ];

    public static void VerifyCopilotCliInstallation()
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "copilot",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo)
                ?? throw new InvalidOperationException("Failed to start copilot CLI process.");
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
                throw new InvalidOperationException(
                    $"Copilot CLI is not installed or not accessible. Error: {error}");
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                "Copilot CLI is not installed or not accessible.", ex);
        }
    }

    /// <summary>
    /// Full two-stage digest generation pipeline.
    /// Stage 1: Fetch top 50 stories from the last 24 hours and save as unprocessed_data_news_digest.json.
    /// Stage 2: Create an ephemeral Copilot session that reads the raw data, applies user interests,
    ///          and writes digest.json.
    /// </summary>
    public async Task GenerateNewsDigestAsync(
        IProgress<DigestGenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {

            var allowedDirectory = Path.Combine(
                vaultFileService.BasePath ?? throw new InvalidOperationException("Vault path is not set."),
                AppFolderNames.NEWS_DIGEST_FOLDER);
            var agentCompletion = new TaskCompletionSource<bool>();
            var agentProgress = 60;

            await using var client = new CopilotClient(new CopilotClientOptions
            {
                Cwd = allowedDirectory
            });

            await using var session = await client.CreateSessionAsync(new SessionConfig
            {
                Model = GetConfiguredModel(),
                Tools = [
                    AIFunctionFactory.Create(copilotFunctions.ScrapeArticleAsync, "scrape_article"),
                    AIFunctionFactory.Create(copilotFunctions.ReadCommentsAsync, "read_comments")
                    ],
                Streaming = true,
                InfiniteSessions = new InfiniteSessionConfig { Enabled = false },
                OnPermissionRequest = (request, invocation) =>
                    PromptPermission(request, invocation, allowedDirectory),
                SystemMessage = new SystemMessageConfig
                {
                    Content = BuildSystemMessageForDigestAgent(),
                    Mode = SystemMessageMode.Append
                }
            }, cancellationToken);

            // Subscribe to session events for real-time progress
            using var subscription = session.On(evt =>
            {
                switch (evt)
                {
                    case ToolExecutionStartEvent toolStart:
                        Debug.WriteLine($"[DigestGen] Agent tool: {toolStart.Data.ToolName}");
                        progress?.Report(DigestGenerationProgress.Stage(agentProgress,
                            $"Agent: executing {toolStart.Data.ToolName}..."));
                        break;

                    case ToolExecutionCompleteEvent toolComplete:
                        Debug.WriteLine($"[DigestGen] Agent tool completed");
                        progress?.Report(DigestGenerationProgress.Stage(agentProgress,
                            "Agent: processing results..."));
                        break;

                    case AssistantMessageDeltaEvent:
                        progress?.Report(DigestGenerationProgress.Stage(agentProgress,
                                "Agent is writing the digest..."));
                        break;

                    case SessionErrorEvent errorEvent:
                        agentCompletion.TrySetException(
                            new InvalidOperationException(
                                $"Agent error: {errorEvent.Data.Message}"));
                        break;

                    case SessionIdleEvent:
                        agentCompletion.TrySetResult(true);
                        break;
                }
            });

            var prompt = BuildNewsDigestPrompt();
            await session.SendAsync(new MessageOptions { Prompt = prompt }, cancellationToken);

            // Wait for the agent to finish, or cancel
            using var ctRegistration = cancellationToken.Register(() =>
            {
                _ = session.AbortAsync();
                agentCompletion.TrySetCanceled(cancellationToken);
            });

            await agentCompletion.Task;

            progress?.Report(DigestGenerationProgress.Stage(95, "Loading generated digest..."));
            Debug.WriteLine("[DigestGen] Agent session completed");
        }
        catch (OperationCanceledException)
        {
            progress?.Report(DigestGenerationProgress.Stage(0, "Digest generation cancelled."));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error generating digest: {ex}");
            progress?.Report(DigestGenerationProgress.Error(
                $"Failed to generate digest: {ex.Message}"));
        }
    }

    /// <summary>
    /// Generates an AI insight for a single story using non-streaming completion.
    /// Returns the insight as a markdown string, or throws on failure.
    /// </summary>
    /// <param name="storyId">The HN story ID to generate insight for.</param>
    /// <param name="progress">Optional progress reporter for UI updates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<string> GenerateStoryInsightAsync(
        int storyId,
        IProgress<InsightGenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var allowedDirectory = Path.Combine(
            vaultFileService.BasePath ?? throw new InvalidOperationException("Vault path is not set."),
            AppFolderNames.STORIES_FOLDER);

        progress?.Report(InsightGenerationProgress.Stage(70, "Connecting to Copilot..."));

        await using var client = new CopilotClient(new CopilotClientOptions
        {
            Cwd = allowedDirectory
        });

        await using var session = await client.CreateSessionAsync(new SessionConfig
        {
            Model = GetConfiguredModel(),
            Streaming = false,
            InfiniteSessions = new InfiniteSessionConfig { Enabled = false },
            OnPermissionRequest = (request, invocation) =>
                PromptPermission(request, invocation, allowedDirectory),
            SystemMessage = new SystemMessageConfig
            {
                Content = BuildSystemMessageForStoryInsightAgent(),
                Mode = SystemMessageMode.Append
            }
        }, cancellationToken);

        progress?.Report(InsightGenerationProgress.Stage(80, "Generating insight..."));

        var prompt = BuildInsightPrompt(storyId);
        
        // Use a reasonable timeout; cancellation is handled at session level
        cancellationToken.ThrowIfCancellationRequested();
        var response = await session.SendAndWaitAsync(new MessageOptions { Prompt = prompt });

        if (response?.Data.Content == null)
        {
            throw new InvalidOperationException("Failed to generate insight: no response received.");
        }

        progress?.Report(InsightGenerationProgress.Complete(response.Data.Content));
        Debug.WriteLine($"[StoryInsight] Generated insight for story {storyId}");

        return response.Data.Content;
    }

        private static string BuildInsightPrompt(int storyId) => $"""
            Read `{storyId}.md` in the current directory and produce the insight in markdown.
            Follow the instructions in Agent.md in the current directory exactly.
            Return ONLY the final markdown content. Do not add any prefatory sentence, commentary, code fences, or notes before `## TL;DR`.
            """;

    private string GetConfiguredModel()
    {
        var configuredModel = settingsService.CopilotModel;
        if (string.IsNullOrWhiteSpace(configuredModel))
        {
            return DefaultCopilotModel;
        }

        return SupportedModels.Contains(configuredModel)
            ? configuredModel
            : DefaultCopilotModel;
    }

    /// <summary>
    /// Builds the system message for story insight generation in the stories directory.
    /// </summary>
    private static string BuildSystemMessageForStoryInsightAgent()
    {
        return $"""
             You are an AI agent running in a secure sandbox environment with access only to the {AppFolderNames.STORIES_FOLDER} folder.
             You have read access to story markdown files in this folder and cannot access anything outside it.
             Follow the instructions in Agent.md in the current directory exactly.
             Output must be markdown only and must start directly with `## TL;DR`.
             Do NOT include intro phrases, process narration, or any text before the first heading.
             """;
    }

    /// <summary>
    /// Builds the system message pointing the agent to the Agent.md instructions
    /// and constraining its output to the news_digest directory.
    /// </summary>
    private static string BuildSystemMessageForDigestAgent()
    {
        return $"""
             You are an AI agent running in a secure sandbox environment with access only to the {AppFolderNames.NEWS_DIGEST_FOLDER} folder.
             You have read/write access to files in this folder, but cannot access anything outside it.
             Follow the instructions in Agent.md in the current directory exactly.
             Your ONLY task is to read {AppFileNames.UNPROCESSED_DIGEST_DATA_FILE_NAME}, apply the user's interests
             to group and rank stories, and call the write_news_digest tool with the JSON output.
             Do NOT modify {AppFileNames.UNPROCESSED_DIGEST_DATA_FILE_NAME}.
             The JSON schema is specified in Agent.md — follow it precisely.
             """;
    }

    /// <summary>
    /// Builds the user prompt with the configured interests for the agent.
    /// </summary>
    private string BuildNewsDigestPrompt()
    {
        var interests = settingsService.UserInterests;
        var interestLines = string.Join("\n", interests.Select(i => $"- {i.Name}: {i.Description}"));

        return $"""
            Generate a news digest from the stories in {AppFileNames.UNPROCESSED_DIGEST_DATA_FILE_NAME}.

            The user's configured interests are:
            {interestLines}

            Group the stories by these interests, rank them by trending score (points + comments + recency), and save the JSON result following the exact schema in Agent.md.
            Total amount of selected story per group should not exceed {settingsService.MaxStoriesPerDigestGroup}.
            Each story can belong to exactly one interest group.
            Omit interest groups with no matching stories.
            Today's date is {DateTime.UtcNow:yyyy-MM-dd}.
            """;
    }

    private static Task<PermissionRequestResult> PromptPermission(
        PermissionRequest request,
        PermissionInvocation invocation,
        string allowedDir)
    {
        if (request.ExtensionData != null)
        {
            foreach (var kvp in request.ExtensionData)
            {
                var pathValue = kvp.Value?.ToString();

                // Only validate keys that look like path/file references and have a real value
                if (string.IsNullOrWhiteSpace(pathValue))
                    continue;

                if (string.Equals(pathValue, "[]", StringComparison.Ordinal))
                    continue;

                if (kvp.Key.Contains("path", StringComparison.OrdinalIgnoreCase) ||
                    kvp.Key.Contains("file", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var baseDir = Path.GetFullPath(allowedDir);
                        var fullPath = Path.IsPathRooted(pathValue)
                            ? Path.GetFullPath(pathValue)
                            : Path.GetFullPath(pathValue, baseDir);
                        var normalizedBase = Path.TrimEndingDirectorySeparator(baseDir);
                        var allowedPrefix = normalizedBase + Path.DirectorySeparatorChar;
                        var isAllowed = string.Equals(fullPath, normalizedBase, StringComparison.OrdinalIgnoreCase)
                            || fullPath.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase);

                        if (!isAllowed)
                        {
                            Debug.WriteLine($"[BLOCKED] Path outside allowed directory: {pathValue}");
                            return Task.FromResult(new PermissionRequestResult
                            {
                                Kind = "denied-interactively-by-user"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[BLOCKED] Invalid path value '{pathValue}': {ex.Message}");
                        return Task.FromResult(new PermissionRequestResult
                        {
                            Kind = "denied-interactively-by-user"
                        });
                    }
                }
            }
        }

        Debug.WriteLine($"[Session {invocation.SessionId}] [Approved] {request.Kind}");
        return Task.FromResult(new PermissionRequestResult { Kind = "approved" });
    }
}
