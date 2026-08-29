using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace HNReader.Core.Services.Logging;

/// <summary>
/// Owns the on-disk file handle for the active log file. Pure I/O: rotation,
/// size enforcement, and TTL purge. A single instance is owned by <see cref="Logger"/>.
/// </summary>
internal sealed class LogFileWriter : IDisposable
{
    private readonly object _gate = new();
    private FileStream? _stream;
    private StreamWriter? _writer;
    private string? _currentPath;
    private long _currentBytes;

    public string LogDirectory { get; }
    public string? CurrentFilePath => _currentPath;

    public LogFileWriter(string logDirectory)
    {
        LogDirectory = logDirectory;
        Directory.CreateDirectory(logDirectory);
    }

    /// <summary>Open a new log file. Called once at startup and on each rotation.</summary>
    public void OpenNew(string path)
    {
        lock (_gate)
        {
            CloseCore();
            _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            _writer = new StreamWriter(_stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            {
                AutoFlush = false,
            };
            _currentPath = path;
            _currentBytes = 0;
        }
    }

    /// <summary>Write a single line. Returns true if rotation happened during this write.</summary>
    public bool WriteLine(string line, long maxBytes)
    {
        lock (_gate)
        {
            if (_writer is null || _currentPath is null) return false;

            var bytes = Encoding.UTF8.GetByteCount(line) + 1; // +1 for newline
            if (_currentBytes + bytes > maxBytes && _currentBytes > 0)
            {
                CloseCore();
                return true;
            }

            try
            {
                _writer.WriteLine(line);
                _writer.Flush();
                _currentBytes += bytes;
                return false;
            }
            catch
            {
                CloseCore();
                return false;
            }
        }
    }

    public void Flush()
    {
        lock (_gate)
        {
            try { _writer?.Flush(); }
            catch { /* swallow — best effort */ }
        }
    }

    /// <summary>
    /// Close the current file (if any) and open a fresh one at <paramref name="newPath"/>.
    /// Used to release the OS handle on the active log so callers can read/copy the
    /// now-closed file (e.g. log-export zipping).
    /// </summary>
    public void Rotate(string newPath)
    {
        lock (_gate)
        {
            CloseCore();
            OpenNew(newPath);
        }
    }

    /// <summary>Delete log files older than <paramref name="ttl"/>, keeping at most <paramref name="maxFiles"/> total.</summary>
    public void PurgeExpired(TimeSpan ttl, int maxFiles)
    {
        try
        {
            var cutoff = DateTime.UtcNow - ttl;
            var files = new DirectoryInfo(LogDirectory)
                .EnumerateFiles("*.log", SearchOption.TopDirectoryOnly)
                .ToList();

            foreach (var f in files.Where(f => f.LastWriteTimeUtc < cutoff))
            {
                TryDelete(f);
            }

            var remaining = new DirectoryInfo(LogDirectory)
                .EnumerateFiles("*.log", SearchOption.TopDirectoryOnly)
                .OrderBy(f => f.LastWriteTimeUtc)
                .ToList();
            var overflow = remaining.Count - maxFiles;
            if (overflow > 0)
            {
                foreach (var f in remaining.Take(overflow))
                {
                    TryDelete(f);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Logger] PurgeExpired failed: {ex.Message}");
        }
    }

    private static void TryDelete(FileInfo f)
    {
        try { f.Delete(); }
        catch (Exception ex) { Debug.WriteLine($"[Logger] Could not delete {f.Name}: {ex.Message}"); }
    }

    private void CloseCore()
    {
        try { _writer?.Flush(); } catch { }
        try { _writer?.Dispose(); } catch { }
        try { _stream?.Dispose(); } catch { }
        _writer = null;
        _stream = null;
        _currentPath = null;
        _currentBytes = 0;
    }

    public void Dispose() => CloseCore();

    // Convenience for callers
    internal static string FormatException(Exception ex)
    {
        var sb = new StringBuilder();
        sb.Append(ex.GetType().FullName).Append(": ").Append(ex.Message);
        var inner = ex.InnerException;
        var depth = 0;
        while (inner != null && depth < 5)
        {
            sb.Append(" --> ").Append(inner.GetType().FullName).Append(": ").Append(inner.Message);
            inner = inner.InnerException;
            depth++;
        }
        sb.Append("   ").Append(ex.StackTrace);
        return sb.ToString();
    }

    internal static string FormatContext(IReadOnlyList<KeyValuePair<string, object?>>? ctx)
    {
        if (ctx is null || ctx.Count == 0) return string.Empty;
        var sb = new StringBuilder();
        for (var i = 0; i < ctx.Count; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(ctx[i].Key).Append('=').Append(FormatValue(ctx[i].Value));
        }
        return sb.ToString();
    }

    private static string FormatValue(object? value)
    {
        if (value is null) return "null";
        if (value is string s) return "\"" + s.Replace("\"", "\\\"") + "\"";
        if (value is bool b) return b ? "true" : "false";
        if (value is IFormattable f) return f.ToString(null, CultureInfo.InvariantCulture);
        return value.ToString() ?? "null";
    }
}
