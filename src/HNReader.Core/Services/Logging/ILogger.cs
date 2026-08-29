using System;
using System.Collections.Generic;

namespace HNReader.Core.Services.Logging;

/// <summary>
/// Application logger. Writes to rolling files in <c>%TEMP%\HNReader\logs\</c>
/// with a TTL purge on startup. Thread-safe; UI-thread calls are non-blocking.
/// </summary>
public interface ILogger : IDisposable
{
    /// <summary>Absolute path of the directory where log files are written.</summary>
    string LogDirectory { get; }

    /// <summary>Absolute path of the current log file, or <c>null</c> until the first write.</summary>
    string? CurrentLogFilePath { get; }

    /// <summary>Current minimum log level. Entries below this level are dropped.</summary>
    LogLevel MinLevel { get; set; }

    void LogTrace(string category, string message, Exception? exception = null,
        IReadOnlyList<KeyValuePair<string, object?>>? context = null);

    void LogDebug(string category, string message, Exception? exception = null,
        IReadOnlyList<KeyValuePair<string, object?>>? context = null);

    void LogInformation(string category, string message, Exception? exception = null,
        IReadOnlyList<KeyValuePair<string, object?>>? context = null);

    void LogWarning(string category, string message, Exception? exception = null,
        IReadOnlyList<KeyValuePair<string, object?>>? context = null);

    void LogError(string category, string message, Exception? exception = null,
        IReadOnlyList<KeyValuePair<string, object?>>? context = null);

    void LogFatal(string category, string message, Exception? exception = null,
        IReadOnlyList<KeyValuePair<string, object?>>? context = null);

    /// <summary>Force the current file to flush and close cleanly. Awaitable.</summary>
    System.Threading.Tasks.Task FlushAsync();

    /// <summary>
    /// Drain pending entries to the current file, close its OS handle, and open a
    /// fresh log file. Use before reading/copying the just-closed file (e.g. log
    /// export zipping) to avoid sharing-violation errors.
    /// </summary>
    System.Threading.Tasks.Task RotateAsync();
}
