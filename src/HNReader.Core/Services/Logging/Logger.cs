using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using HNReader.Core.Constants;

namespace HNReader.Core.Services.Logging;

/// <summary>
/// File logger implementation. All public <c>LogX</c> methods are non-blocking;
/// writes are serialized on a single background consumer.
/// </summary>
public sealed class Logger : ILogger
{
    private readonly LogFileWriter _writer;
    private readonly Channel<LogEntry> _channel;
    private readonly Task _consumer;
    private readonly CancellationTokenSource _cts = new();
    private long _droppedSinceLastWarning;
    private long _currentMaxBytes;
    private string? _currentPath;
    private readonly object _pathLock = new();
    private LogLevel _minLevel = LogLevel.Information;

    public string LogDirectory => _writer.LogDirectory;

    public string? CurrentLogFilePath
    {
        get { lock (_pathLock) return _currentPath; }
    }

    public LogLevel MinLevel
    {
        get => _minLevel;
        set => _minLevel = value;
    }

    public Logger() : this(LoggingPaths.ResolveLogDirectory()) { }

    public Logger(string logDirectory)
    {
        _writer = new LogFileWriter(logDirectory);
        _currentMaxBytes = AppFileNames.LOG_MAX_FILE_BYTES;

        // First-write → create today's file. Purge any expired siblings up front.
        _writer.PurgeExpired(AppFileNames.LOG_TTL, AppFileNames.LOG_MAX_FILES);
        var path = Path.Combine(logDirectory, LoggingPaths.BuildLogFileName(DateTimeOffset.UtcNow));
        _writer.OpenNew(path);
        lock (_pathLock) _currentPath = path;

        _channel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(1024)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false,
        });

        _consumer = Task.Run(() => ConsumeAsync(_cts.Token));
    }

    public void LogTrace(string category, string message, Exception? exception = null,
        IReadOnlyList<KeyValuePair<string, object?>>? context = null)
        => Enqueue(LogLevel.Trace, category, message, exception, context);

    public void LogDebug(string category, string message, Exception? exception = null,
        IReadOnlyList<KeyValuePair<string, object?>>? context = null)
        => Enqueue(LogLevel.Debug, category, message, exception, context);

    public void LogInformation(string category, string message, Exception? exception = null,
        IReadOnlyList<KeyValuePair<string, object?>>? context = null)
        => Enqueue(LogLevel.Information, category, message, exception, context);

    public void LogWarning(string category, string message, Exception? exception = null,
        IReadOnlyList<KeyValuePair<string, object?>>? context = null)
        => Enqueue(LogLevel.Warning, category, message, exception, context);

    public void LogError(string category, string message, Exception? exception = null,
        IReadOnlyList<KeyValuePair<string, object?>>? context = null)
        => Enqueue(LogLevel.Error, category, message, exception, context);

    public void LogFatal(string category, string message, Exception? exception = null,
        IReadOnlyList<KeyValuePair<string, object?>>? context = null)
        => Enqueue(LogLevel.Fatal, category, message, exception, context);

    private void Enqueue(LogLevel level, string category, string message,
        Exception? exception, IReadOnlyList<KeyValuePair<string, object?>>? context)
    {
        if (level < _minLevel || _minLevel == LogLevel.Off) return;

        var entry = BuildEntry(level, category, message, exception, context);
        if (!_channel.Writer.TryWrite(entry))
        {
            Interlocked.Increment(ref _droppedSinceLastWarning);
        }
    }

    private LogEntry BuildEntry(LogLevel level, string category, string message,
        Exception? exception, IReadOnlyList<KeyValuePair<string, object?>>? context)
    {
        var ts = DateTimeOffset.UtcNow;
        var tid = Environment.CurrentManagedThreadId;
        var line = RenderLine(ts, level, category, tid, message, exception, context);
        return new LogEntry(ts, level, category, tid, message, exception, context, line);
    }

    private static string RenderLine(DateTimeOffset ts, LogLevel level, string category, int tid,
        string message, Exception? exception, IReadOnlyList<KeyValuePair<string, object?>>? context)
    {
        var sb = new System.Text.StringBuilder(128);
        sb.Append(ts.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
        sb.Append(" [").Append(LogEntry.FormatLevel(level)).Append("]");
        sb.Append(" [TID:").Append(tid.ToString(CultureInfo.InvariantCulture)).Append(']');
        sb.Append(' ').Append(category);
        sb.Append(" - ").Append(message);
        var ctxText = LogFileWriter.FormatContext(context);
        if (ctxText.Length > 0) sb.Append(" { ").Append(ctxText).Append(" }");
        if (exception != null) sb.Append('\n').Append(LogFileWriter.FormatException(exception));
        return sb.ToString();
    }

    private async Task ConsumeAsync(CancellationToken token)
    {
        try
        {
            await foreach (var entry in _channel.Reader.ReadAllAsync(token).ConfigureAwait(false))
            {
            try
            {
                var rotated = _writer.WriteLine(entry.Line, _currentMaxBytes);
                if (rotated)
                {
                    var path = Path.Combine(LogDirectory, LoggingPaths.BuildLogFileName(DateTimeOffset.UtcNow));
                    _writer.OpenNew(path);
                    lock (_pathLock) _currentPath = path;
                }
            }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Logger] Write failed: {ex.Message}");
                }

                if (Interlocked.Exchange(ref _droppedSinceLastWarning, 0) > 0)
                {
                    // Single replacement warning; we lost a line to channel pressure.
                    try
                    {
                        _writer.WriteLine(
                            RenderLine(DateTimeOffset.UtcNow, LogLevel.Warning, "Logger", Environment.CurrentManagedThreadId,
                                "log channel overflow; earlier entries were dropped", null, null),
                            _currentMaxBytes);
                    }
                    catch { /* swallow */ }
                }
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
    }

    public Task FlushAsync()
    {
        // Close + reopen the same path so the OS releases the file handle. This
        // lets users copy/delete the active log without closing the app.
        var path = CurrentLogFilePath;
        if (path is null) return Task.CompletedTask;

        return Task.Run(() =>
        {
            try
            {
                _writer.Flush();
                // Drain anything pending so the file is fully written.
                while (_channel.Reader.TryRead(out _)) { }
                _writer.Flush();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Logger] FlushAsync failed: {ex.Message}");
            }
        });
    }

    public Task RotateAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                // Drain pending entries to the *current* file before we close it.
                _writer.Flush();
                while (_channel.Reader.TryRead(out _)) { }
                _writer.Flush();

                var newPath = Path.Combine(LogDirectory, LoggingPaths.BuildLogFileName(DateTimeOffset.UtcNow));
                _writer.Rotate(newPath);
                lock (_pathLock) _currentPath = newPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Logger] RotateAsync failed: {ex.Message}");
            }
        });
    }

    public void Dispose()
    {
        try { _channel.Writer.TryComplete(); } catch { }
        try { _cts.Cancel(); } catch { }
        try { _consumer.Wait(TimeSpan.FromSeconds(2)); } catch { }
        try { _writer.Dispose(); } catch { }
        _cts.Dispose();
    }
}
