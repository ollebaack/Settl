namespace Settl.Api.Services;

/// <summary>
/// Dev-only: appends structured logs to a local file so tools without a view into the
/// console (Claude Code) can read what the API is doing. Truncated on startup — same
/// "fresh every pnpm dev start" precedent as the dev Postgres reset — so a log always
/// reflects only the current session, never grows unbounded.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly StreamWriter _writer;
    private readonly Lock _lock = new();

    public FileLoggerProvider(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _writer = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true
        };
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, _writer, _lock);

    public void Dispose() => _writer.Dispose();

    private sealed class FileLogger(string category, StreamWriter writer, Lock @lock) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var line = $"[{DateTimeOffset.Now:HH:mm:ss.fff}] [{logLevel}] {category}: {formatter(state, exception)}";
            if (exception is not null) line += Environment.NewLine + exception;

            lock (@lock)
            {
                writer.WriteLine(line);
            }
        }
    }
}
