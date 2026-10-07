using Microsoft.Extensions.Logging;
using TaskManager.Logging;
using TaskManager.Models;
using TaskManager.Tracing;

namespace TaskManager.Services
{
    public class OperationResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = "";

        public static OperationResult Ok(string message) => new() { Success = true, Message = message };
        public static OperationResult Error(string message) => new() { Success = false, Message = message };
    }

    // бизнес-логика: создание, удаление, вывод списка задач
    public class TaskService
    {
        public const int MaxTitleLength = 100;

        private readonly TaskRepository _repository;
        private readonly ILogger<TaskService> _logger;
        private readonly List<TaskItem> _tasks;

        public TaskService(TaskRepository repository, ILogger<TaskService> logger)
        {
            _repository = repository;
            _logger = logger;
            _tasks = _repository.Load();
        }

        // ---------- создание ----------

        public OperationResult Add(string? title, TaskPriority priority = TaskPriority.Medium)
        {
            using var trace = OperationTrace.Begin("AddTask");

            trace.Step("проверка названия");
            title = title?.Trim() ?? "";

            if (title.Length == 0)
            {
                _logger.LogWarning(LogEvents.ValidationFailed, "Попытка создать задачу с пустым названием");
                trace.Fail("пустое название");
                return OperationResult.Error("Название задачи не может быть пустым");
            }

            if (title.Length > MaxTitleLength)
            {
                _logger.LogWarning(LogEvents.ValidationFailed, "Слишком длинное название задачи: {Length} символов", title.Length);
                trace.Fail("слишком длинное название");
                return OperationResult.Error($"Название не должно быть длиннее {MaxTitleLength} символов");
            }

            trace.Step("вычисление нового Id");
            int id = _tasks.Count == 0 ? 1 : _tasks.Max(t => t.Id) + 1;

            trace.Step("создание объекта задачи");
            var task = new TaskItem
            {
                Id = id,
                Title = title,
                Priority = priority,
                CreatedAt = DateTime.Now
            };
            _tasks.Add(task);

            trace.Step("сохранение в хранилище");
            _repository.Save(_tasks);

            _logger.LogInformation(LogEvents.TaskCreated, "Создана задача #{Id} \"{Title}\" с приоритетом {Priority}",
                task.Id, task.Title, task.Priority);

            trace.Success($"задача #{task.Id}");
            return OperationResult.Ok($"Задача #{task.Id} добавлена");
        }

        // ---------- удаление ----------

        public OperationResult Delete(int id)
        {
            using var trace = OperationTrace.Begin("DeleteTask");

            trace.Step($"поиск задачи #{id}");
            var task = _tasks.FirstOrDefault(t => t.Id == id);

            if (task == null)
            {
                _logger.LogWarning(LogEvents.TaskNotFound, "Удаление: задача #{Id} не найдена", id);
                trace.Fail("задача не найдена");
                return OperationResult.Error($"Задача #{id} не найдена");
            }

            trace.Step("удаление из списка");
            _tasks.Remove(task);

            trace.Step("сохранение в хранилище");
            _repository.Save(_tasks);

            _logger.LogInformation(LogEvents.TaskDeleted, "Удалена задача #{Id} \"{Title}\"", task.Id, task.Title);

            trace.Success($"удалена #{id}");
            return OperationResult.Ok($"Задача #{id} \"{task.Title}\" удалена");
        }

        // ---------- выполнение (дополнительно) ----------

        public OperationResult Complete(int id)
        {
            using var trace = OperationTrace.Begin("CompleteTask");

            trace.Step($"поиск задачи #{id}");
            var task = _tasks.FirstOrDefault(t => t.Id == id);

            if (task == null)
            {
                _logger.LogWarning(LogEvents.TaskNotFound, "Выполнение: задача #{Id} не найдена", id);
                trace.Fail("задача не найдена");
                return OperationResult.Error($"Задача #{id} не найдена");
            }

            if (task.IsDone)
            {
                _logger.LogWarning(LogEvents.ValidationFailed, "Задача #{Id} уже была выполнена", id);
                trace.Fail("уже выполнена");
                return OperationResult.Error($"Задача #{id} уже выполнена");
            }

            trace.Step("отметка выполнения");
            task.IsDone = true;
            task.CompletedAt = DateTime.Now;

            trace.Step("сохранение в хранилище");
            _repository.Save(_tasks);

            _logger.LogInformation(LogEvents.TaskCompleted, "Задача #{Id} \"{Title}\" выполнена", task.Id, task.Title);

            trace.Success($"выполнена #{id}");
            return OperationResult.Ok($"Задача #{id} отмечена выполненной");
        }

        // ---------- вывод списка ----------

        public IReadOnlyList<TaskItem> GetAll()
        {
            using var trace = OperationTrace.Begin("ListTasks");

            trace.Step("сортировка: сначала невыполненные, затем по приоритету");
            var result = _tasks
                .OrderBy(t => t.IsDone)
                .ThenByDescending(t => t.Priority)
                .ThenBy(t => t.Id)
                .ToList();

            int done = result.Count(t => t.IsDone);
            _logger.LogInformation(LogEvents.TasksListed, "Запрошен список задач: всего {Total}, выполнено {Done}",
                result.Count, done);

            trace.Success($"{result.Count} задач");
            return result;
        }
    }
}
