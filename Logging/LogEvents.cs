using Microsoft.Extensions.Logging;

namespace TaskManager.Logging
{
    // коды событий: по ним удобно искать нужные записи в логе
    public static class LogEvents
    {
        public static readonly EventId AppStarted = new(1000, nameof(AppStarted));
        public static readonly EventId AppStopped = new(1001, nameof(AppStopped));

        public static readonly EventId TaskCreated = new(2001, nameof(TaskCreated));
        public static readonly EventId TaskDeleted = new(2002, nameof(TaskDeleted));
        public static readonly EventId TasksListed = new(2003, nameof(TasksListed));
        public static readonly EventId TaskCompleted = new(2004, nameof(TaskCompleted));

        public static readonly EventId ValidationFailed = new(3001, nameof(ValidationFailed));
        public static readonly EventId TaskNotFound = new(3002, nameof(TaskNotFound));
        public static readonly EventId InvalidInput = new(3003, nameof(InvalidInput));

        public static readonly EventId StorageLoaded = new(4001, nameof(StorageLoaded));
        public static readonly EventId StorageSaved = new(4002, nameof(StorageSaved));
        public static readonly EventId StorageError = new(4003, nameof(StorageError));
    }
}
