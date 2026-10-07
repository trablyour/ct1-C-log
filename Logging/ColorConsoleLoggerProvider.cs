using Microsoft.Extensions.Logging;

namespace TaskManager.Logging
{
    // выводит логи в консоль сразу (синхронно) и разными цветами по уровню
    public sealed class ColorConsoleLoggerProvider : ILoggerProvider
    {
        private readonly LogLevel _minLevel;
        internal static readonly object ConsoleLock = new();

        public ColorConsoleLoggerProvider(LogLevel minLevel)
        {
            _minLevel = minLevel;
        }

        public ILogger CreateLogger(string categoryName) => new ColorConsoleLogger(categoryName, _minLevel);

        public void Dispose()
        {
        }

        private sealed class ColorConsoleLogger : ILogger
        {
            private readonly string _category;
            private readonly LogLevel _minLevel;

            public ColorConsoleLogger(string category, LogLevel minLevel)
            {
                _category = category;
                _minLevel = minLevel;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= _minLevel;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel))
                    return;

                string message = formatter(state, exception);
                string time = DateTime.Now.ToString("HH:mm:ss");

                lock (ConsoleLock)
                {
                    var old = Console.ForegroundColor;

                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write($"  {time} ");

                    Console.ForegroundColor = logLevel switch
                    {
                        LogLevel.Warning => ConsoleColor.Yellow,
                        LogLevel.Error or LogLevel.Critical => ConsoleColor.Red,
                        LogLevel.Information => ConsoleColor.Green,
                        _ => ConsoleColor.Gray
                    };
                    Console.Write($"[{LogFormatter.Level(logLevel)}] ");

                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine(message);

                    if (exception != null)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"         {exception.GetType().Name}: {exception.Message}");
                    }

                    Console.ForegroundColor = old;
                }
            }
        }
    }
}
