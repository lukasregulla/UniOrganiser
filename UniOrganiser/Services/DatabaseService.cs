using System.IO;
using SQLite;
using UniOrganiser.Models;

namespace UniOrganiser.Services;

public class DatabaseService : IDisposable
{
    private readonly SQLiteConnection _db;

    public DatabaseService()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UniOrganiser");
        Directory.CreateDirectory(folder);
        var dbPath = Path.Combine(folder, "uniorganiser.db");

        _db = new SQLiteConnection(dbPath);
        _db.CreateTable<Subject>();
        _db.CreateTable<TaskItem>();
        _db.CreateTable<RecurrenceRule>();
    }

    // --- Subject CRUD ---

    public List<Subject> GetSubjects() =>
        _db.Table<Subject>().OrderBy(s => s.Name).ToList();

    public Subject? GetSubject(int id) => _db.Find<Subject>(id);

    public int SaveSubject(Subject subject) =>
        subject.Id == 0 ? _db.Insert(subject) : _db.Update(subject);

    public void DeleteSubject(int id)
    {
        // No FK cascade in sqlite-net: detach any tasks referencing this subject first.
        var affectedTasks = _db.Table<TaskItem>().Where(t => t.SubjectId == id).ToList();
        foreach (var task in affectedTasks)
        {
            task.SubjectId = null;
            _db.Update(task);
        }

        _db.Delete<Subject>(id);
    }

    // --- TaskItem CRUD ---

    public List<TaskItem> GetTasks() => _db.Table<TaskItem>().ToList();

    public List<TaskItem> GetTasksInRange(DateTime from, DateTime to) =>
        _db.Table<TaskItem>().Where(t => t.DueDate >= from.Date && t.DueDate <= to.Date).ToList();

    public TaskItem? GetTask(int id) => _db.Find<TaskItem>(id);

    public int SaveTask(TaskItem task) =>
        task.Id == 0 ? _db.Insert(task) : _db.Update(task);

    public void DeleteTask(int id) => _db.Delete<TaskItem>(id);

    // --- RecurrenceRule CRUD ---

    public RecurrenceRule? GetRule(int id) => _db.Find<RecurrenceRule>(id);

    public List<RecurrenceRule> GetRules() => _db.Table<RecurrenceRule>().ToList();

    public int SaveRule(RecurrenceRule rule) =>
        rule.Id == 0 ? _db.Insert(rule) : _db.Update(rule);

    public void DeleteRule(int id) => _db.Delete<RecurrenceRule>(id);

    public List<TaskItem> GetOccurrences(int ruleId) =>
        _db.Table<TaskItem>().Where(t => t.RecurrenceRuleId == ruleId).OrderBy(t => t.DueDate).ToList();

    public void Dispose() => _db.Close();
}
