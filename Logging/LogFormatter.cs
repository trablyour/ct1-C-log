using Microsoft.Extensions.Logging;

namespace TaskManager.Logging
{
    public static class LogFormatter
    {
        public static string Level(LogLevel level) => level switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => "???"
        };

        // TaskManager.Services.TaskService -> TaskService
        public static string ShortCategory(string category)
        {
            int dot = category.LastIndexOf('.');
            return dot >= 0 ? category[(dot + 1)..] : category;
        }

        public static string Format(string category, LogLevel level, EventId eventId, string message, Exception? exception)
        {
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{Level(level)}] ({eventId.Id}) {ShortCategory(category)}: {message}";

            if (exception != null)
                line += Environment.NewLine + "    " + exception.GetType().Name + ": " + exception.Message;

            return line;
        }
    }
}
