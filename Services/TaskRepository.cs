using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Microsoft.Extensions.Logging;
using TaskManager.Logging;
using TaskManager.Models;
using TaskManager.Tracing;

namespace TaskManager.Services
{
    // хранение задач в JSON-файле, чтобы они не пропадали между запусками
    public class TaskRepository
    {
        private readonly string _filePath;
        private readonly ILogger<TaskRepository> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
            // чтобы кириллица в файле была читаемой
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

        public TaskRepository(string filePath, ILogger<TaskRepository> logger)
        {
            _filePath = filePath;
            _logger = logger;
        }

        public List<TaskItem> Load()
        {
            using var trace = OperationTrace.Begin("LoadTasks");

            trace.Step($"проверка файла {_filePath}");
            if (!File.Exists(_filePath))
            {
                _logger.LogInformation(LogEvents.StorageLoaded, "Файл задач не найден, начинаем с пустого списка");
                trace.Success("файла нет, список пуст");
                return new List<TaskItem>();
            }

            try
            {
                trace.Step("чтение и разбор JSON");
                string json = File.ReadAllText(_filePath);
                var tasks = JsonSerializer.Deserialize<List<TaskItem>>(json, JsonOptions) ?? new List<TaskItem>();

                _logger.LogInformation(LogEvents.StorageLoaded, "Загружено задач из файла: {Count}", tasks.Count);
                trace.Success($"загружено {tasks.Count}");
                return tasks;
            }
            catch (JsonException ex)
            {
                // файл поврежден: не падаем, а начинаем заново
                _logger.LogError(LogEvents.StorageError, ex, "Файл задач поврежден, будет создан новый");
                trace.Fail("поврежденный JSON");
                return new List<TaskItem>();
            }
        }

        public void Save(List<TaskItem> tasks)
        {
            using var trace = OperationTrace.Begin("SaveTasks");

            try
            {
                trace.Step("сериализация в JSON");
                string json = JsonSerializer.Serialize(tasks, JsonOptions);

                trace.Step("запись в файл");
                string? folder = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(folder))
                    Directory.CreateDirectory(folder);
                File.WriteAllText(_filePath, json);

                _logger.LogDebug(LogEvents.StorageSaved, "Сохранено задач: {Count}, файл {File}", tasks.Count, _filePath);
                trace.Success($"сохранено {tasks.Count}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(LogEvents.StorageError, ex, "Не удалось сохранить задачи в {File}", _filePath);
                trace.Fail(ex.Message);
                throw;
            }
        }
    }
}
