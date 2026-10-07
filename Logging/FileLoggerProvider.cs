using Microsoft.Extensions.Logging;

namespace TaskManager.Logging
{
    // пишет логи в файл logs/app-ГГГГММДД.log
    public sealed class FileLoggerProvider : ILoggerProvider
    {
        private readonly StreamWriter _writer;
        private readonly LogLevel _minLevel;
        private readonly object _lock = new();

        public string FilePath { get; }

        public FileLoggerProvider(string folder, LogLevel minLevel)
        {
            Directory.CreateDirectory(folder);
            FilePath = Path.Combine(folder, $"app-{DateTime.Now:yyyyMMdd}.log");
            _writer = new StreamWriter(FilePath, append: true) { AutoFlush = true };
            _minLevel = minLevel;
        }

        public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, this);

        internal bool IsEnabled(LogLevel level) => level != LogLevel.None && level >= _minLevel;

        internal void Write(string line)
        {
            lock (_lock)
            {
                _writer.WriteLine(line);
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _writer.Dispose();
            }
        }

        private sealed class FileLogger : ILogger
        {
            private readonly string _category;
            private readonly FileLoggerProvider _provider;

            public FileLogger(string category, FileLoggerProvider provider)
            {
                _category = category;
                _provider = provider;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => _provider.IsEnabled(logLevel);

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel))
                    return;

                _provider.Write(LogFormatter.Format(_category, logLevel, eventId, formatter(state, exception), exception));
            }
        }
    }
}
