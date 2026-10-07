using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using TaskManager.Logging;
using TaskManager.Models;
using TaskManager.Services;
using TaskManager.Tracing;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

string baseFolder = Directory.GetCurrentDirectory();
string logsFolder = Path.Combine(baseFolder, "logs");
string dataFile = Path.Combine(baseFolder, "data", "tasks.json");

// ---------- логирование: консоль (Information+) и файл (Debug+) ----------
var fileLogger = new FileLoggerProvider(logsFolder, LogLevel.Debug);

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.SetMinimumLevel(LogLevel.Debug);
    builder.AddProvider(new ColorConsoleLoggerProvider(LogLevel.Information));
    builder.AddProvider(fileLogger);
});

var log = loggerFactory.CreateLogger("TaskManager.Program");

// ---------- трассировка: файл всегда, консоль включается в меню ----------
string traceFile = Path.Combine(logsFolder, $"trace-{DateTime.Now:yyyyMMdd}.log");
var fileTrace = new TextWriterTraceListener(traceFile, "FileTrace");
var consoleTrace = new ColorConsoleTraceListener();

Trace.AutoFlush = true;
Trace.IndentSize = 4;
Trace.Listeners.Add(fileTrace);
Trace.Listeners.Add(consoleTrace);
bool consoleTraceOn = true;

Trace.WriteLine("");
Trace.WriteLine($"========== Запуск {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==========");

log.LogInformation(LogEvents.AppStarted, "Приложение запущено. Логи: {LogFile}, трассировка: {TraceFile}",
    fileLogger.FilePath, traceFile);

var service = new TaskService(
    new TaskRepository(dataFile, loggerFactory.CreateLogger<TaskRepository>()),
    loggerFactory.CreateLogger<TaskService>());

try
{
    if (args.Contains("--demo"))
        RunDemo();
    else
        RunMenu();
}
catch (Exception ex)
{
    log.LogCritical(ex, "Необработанная ошибка, приложение остановлено");
    Console.WriteLine("Критическая ошибка: " + ex.Message);
}
finally
{
    log.LogInformation(LogEvents.AppStopped, "Приложение завершено");
    Trace.WriteLine($"========== Завершение {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==========");
    Trace.Flush();
    fileTrace.Dispose();
    fileLogger.Dispose();
}

// =================== интерактивное меню ===================

void RunMenu()
{
    while (true)
    {
        PrintMenu();
        Console.Write("Выберите пункт: ");
        string? choice = Console.ReadLine()?.Trim();
        Console.WriteLine();

        switch (choice)
        {
            case "1":
                AddTask();
                break;
            case "2":
                PrintTasks(service.GetAll());
                break;
            case "3":
                DeleteTask();
                break;
            case "4":
                CompleteTask();
                break;
            case "5":
                ToggleConsoleTrace();
                break;
            case "6":
                Info($"Логи:        {fileLogger.FilePath}");
                Info($"Трассировка: {traceFile}");
                Info($"Задачи:      {dataFile}");
                break;
            case "0":
                return;
            default:
                log.LogWarning(LogEvents.InvalidInput, "Неизвестный пункт меню: \"{Choice}\"", choice);
                Error("Нет такого пункта меню");
                break;
        }

        Console.WriteLine();
    }
}

void PrintMenu()
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("=========== TaskManager ===========");
    Console.ResetColor();
    Console.WriteLine(" 1. Добавить задачу");
    Console.WriteLine(" 2. Показать задачи");
    Console.WriteLine(" 3. Удалить задачу");
    Console.WriteLine(" 4. Отметить задачу выполненной");
    Console.WriteLine($" 5. Трассировка в консоли: {(consoleTraceOn ? "ВКЛ" : "ВЫКЛ")}");
    Console.WriteLine(" 6. Где лежат логи");
    Console.WriteLine(" 0. Выход");
}

void AddTask()
{
    Console.Write("Название задачи: ");
    string? title = Console.ReadLine();

    Console.Write("Приоритет (1 - низкий, 2 - средний, 3 - высокий) [2]: ");
    string? input = Console.ReadLine()?.Trim();

    var priority = TaskPriority.Medium;
    if (!string.IsNullOrEmpty(input))
    {
        if (int.TryParse(input, out int p) && Enum.IsDefined(typeof(TaskPriority), p))
        {
            priority = (TaskPriority)p;
        }
        else
        {
            log.LogWarning(LogEvents.InvalidInput, "Некорректный приоритет \"{Input}\", использован средний", input);
        }
    }

    ShowResult(service.Add(title, priority));
}

void DeleteTask()
{
    int? id = ReadId("Id задачи для удаления: ");
    if (id != null)
        ShowResult(service.Delete(id.Value));
}

void CompleteTask()
{
    int? id = ReadId("Id выполненной задачи: ");
    if (id != null)
        ShowResult(service.Complete(id.Value));
}

int? ReadId(string prompt)
{
    Console.Write(prompt);
    string? input = Console.ReadLine()?.Trim();

    if (int.TryParse(input, out int id) && id > 0)
        return id;

    log.LogWarning(LogEvents.InvalidInput, "Введен некорректный Id: \"{Input}\"", input);
    Error("Id должен быть положительным числом");
    return null;
}

void ToggleConsoleTrace()
{
    consoleTraceOn = !consoleTraceOn;

    if (consoleTraceOn)
        Trace.Listeners.Add(consoleTrace);
    else
        Trace.Listeners.Remove(consoleTrace);

    log.LogInformation("Трассировка в консоли {State}", consoleTraceOn ? "включена" : "выключена");
}

// =================== демонстрационный сценарий ===================

void RunDemo()
{
    log.LogInformation("Запущен демонстрационный сценарий (--demo)");

    DemoStep(1, "Добавляем три задачи");
    ShowResult(service.Add("Подготовить отчет по практике", TaskPriority.High));
    ShowResult(service.Add("Купить продукты", TaskPriority.Low));
    ShowResult(service.Add("Сделать домашнее задание", TaskPriority.Medium));

    DemoStep(2, "Пытаемся добавить задачу с пустым названием");
    ShowResult(service.Add("   "));

    DemoStep(3, "Выводим список задач");
    var tasks = service.GetAll();
    PrintTasks(tasks);

    DemoStep(4, "Отмечаем первую задачу выполненной");
    if (tasks.Count > 0)
        ShowResult(service.Complete(tasks[0].Id));

    DemoStep(5, "Удаляем последнюю задачу из списка");
    if (tasks.Count > 0)
        ShowResult(service.Delete(tasks[^1].Id));

    DemoStep(6, "Пытаемся удалить несуществующую задачу #999");
    ShowResult(service.Delete(999));

    DemoStep(7, "Итоговый список");
    PrintTasks(service.GetAll());

    Console.WriteLine();
    Info($"Логи:        {fileLogger.FilePath}");
    Info($"Трассировка: {traceFile}");
}

void DemoStep(int number, string title)
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"--- Шаг {number}. {title} ---");
    Console.ResetColor();
}

// =================== вывод ===================

void PrintTasks(IReadOnlyList<TaskItem> list)
{
    if (list.Count == 0)
    {
        Info("Список задач пуст");
        return;
    }

    Console.WriteLine();
    Console.WriteLine($" {"Id",-4} {"Статус",-8} {"Приоритет",-10} {"Создана",-17} Название");
    Console.WriteLine(" " + new string('-', 75));

    foreach (var t in list)
    {
        Console.ForegroundColor = t.IsDone ? ConsoleColor.DarkGray
            : t.Priority == TaskPriority.High ? ConsoleColor.Yellow
            : ConsoleColor.White;

        string status = t.IsDone ? "готово" : "в работе";
        string priority = t.Priority switch
        {
            TaskPriority.High => "высокий",
            TaskPriority.Low => "низкий",
            _ => "средний"
        };

        Console.WriteLine($" {t.Id,-4} {status,-8} {priority,-10} {t.CreatedAt,-17:dd.MM.yyyy HH:mm} {t.Title}");
    }

    Console.ResetColor();
    Console.WriteLine($" Всего: {list.Count}, выполнено: {list.Count(t => t.IsDone)}");
}

void ShowResult(OperationResult result)
{
    if (result.Success)
        Success(result.Message);
    else
        Error(result.Message);
}

void Success(string text) => WriteColored("✓ " + text, ConsoleColor.Green);

void Error(string text) => WriteColored("✗ " + text, ConsoleColor.Red);

void Info(string text) => WriteColored(text, ConsoleColor.Gray);

void WriteColored(string text, ConsoleColor color)
{
    lock (ColorConsoleLoggerProvider.ConsoleLock)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }
}
