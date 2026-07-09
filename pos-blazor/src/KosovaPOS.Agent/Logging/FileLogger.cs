using System.Collections.Concurrent;
using System.Text;

namespace KosovaPOS.Agent.Logging;

/// <summary>
/// Appends log lines to a daily-rolling file. As a Windows service the agent has no
/// console, so without this a fiscal failure at the shop leaves no trace. Deliberately
/// dependency-free (no Serilog) and failure-tolerant: logging must never take the
/// agent down, so every I/O path swallows its exception.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private readonly object _gate = new();
    private readonly string _directory;
    private readonly int _retainDays;
    private DateOnly _openDay;
    private StreamWriter? _writer;

    public FileLoggerProvider(string directory, int retainDays = 14)
    {
        _directory = directory;
        _retainDays = retainDays;
        Directory.CreateDirectory(directory);
    }

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new FileLogger(this, name));

    internal void Write(string line)
    {
        lock (_gate)
        {
            try
            {
                var today = DateOnly.FromDateTime(DateTime.Now);
                if (_writer is null || today != _openDay)
                {
                    _writer?.Dispose();
                    _openDay = today;
                    var path = Path.Combine(_directory, $"agent-{today:yyyyMMdd}.log");
                    _writer = new StreamWriter(
                        new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite),
                        new UTF8Encoding(false))
                    { AutoFlush = true };
                    Prune();
                }
                _writer.WriteLine(line);
            }
            catch
            {
                // A broken log file must not break selling.
            }
        }
    }

    private void Prune()
    {
        var cutoff = DateTime.Now.AddDays(-_retainDays);
        foreach (var f in Directory.EnumerateFiles(_directory, "agent-*.log"))
        {
            try { if (File.GetLastWriteTime(f) < cutoff) File.Delete(f); }
            catch { /* locked or gone */ }
        }
    }

    public void Dispose()
    {
        lock (_gate) { _writer?.Dispose(); _writer = null; }
    }
}

internal sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? ex,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(level)) return;

        var sb = new StringBuilder()
            .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
            .Append(' ').Append(Abbrev(level))
            .Append(' ').Append(category)
            .Append(": ").Append(formatter(state, ex));

        if (ex is not null) sb.AppendLine().Append(ex);

        provider.Write(sb.ToString());
    }

    private static string Abbrev(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "???",
    };
}
