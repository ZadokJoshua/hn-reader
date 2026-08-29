using System;
using System.Collections.Generic;

namespace HNReader.Core.Services.Logging;

internal sealed record LogEntry(
    DateTimeOffset TimestampUtc,
    LogLevel Level,
    string Category,
    int ThreadId,
    string Message,
    Exception? Exception,
    IReadOnlyList<KeyValuePair<string, object?>>? Context,
    string Line)
{
    public static string FormatLevel(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRACE",
        LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "ERROR",
        LogLevel.Fatal => "FATAL",
        _ => level.ToString().ToUpperInvariant(),
    };
}
