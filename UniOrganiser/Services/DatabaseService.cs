using System.IO;
using SQLite;
using UniOrganiser.Models;

namespace UniOrganiser.Services;

public class DatabaseService : IDisposable
{
    private const string DefaultCategoriesSeededKey = "DefaultCategoriesSeeded";
    private const string DefaultSemesterSeededKey = "DefaultSemesterSeeded";

    private static readonly (string Name, string ColourHex)[] DefaultCategories =
    [
        ("Lecture", "#4FA39C"),
        ("Class", "#7D8B99"),
        ("SGTA/Tutorial", "#9AA65C"),
        ("Assessment", "#B08968"),
        ("Test", "#C08497"),
        ("Workshop", "#8E7CC3"),
    ];

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
        _db.CreateTable<Category>();
        _db.CreateTable<TaskItem>();
        _db.CreateTable<RecurrenceRule>();
        _db.CreateTable<AppSetting>();
        _db.CreateTable<Semester>();
        _db.CreateTable<SemesterBreak>();

        SeedDefaultCategories();
        SeedDefaultSemester();
    }

    // --- Settings ---

    public string? GetSetting(string key) => _db.Find<AppSetting>(key)?.Value;

    public void SetSetting(string key, string value) =>
        _db.InsertOrReplace(new AppSetting { Key = key, Value = value });

    // Runs once ever, not once per empty table: deleting every category must
    // not bring the defaults back on the next launch.
    private void SeedDefaultCategories()
    {
        if (GetSetting(DefaultCategoriesSeededKey) == "true") return;

        foreach (var (name, colourHex) in DefaultCategories)
            _db.Insert(new Category { Name = name, ColourHex = colourHex });

        SetSetting(DefaultCategoriesSeededKey, "true");
    }

    // Carries over the term the app used to hardcode, so week labels are
    // unchanged and the Semesters page isn't empty on first launch. Once-ever,
    // same as the category seed.
    private void SeedDefaultSemester()
    {
        if (GetSetting(DefaultSemesterSeededKey) == "true") return;

        var semester = new Semester
        {
            Name = "Session 2 2026",
            StartDate = new DateTime(2026, 7, 27),
            EndDate = new DateTime(2026, 11, 8),
        };
        _db.Insert(semester);

        _db.Insert(new SemesterBreak
        {
            SemesterId = semester.Id,
            Name = "Mid-semester break",
            StartDate = new DateTime(2026, 9, 21),
            EndDate = new DateTime(2026, 10, 4),
        });

        SetSetting(DefaultSemesterSeededKey, "true");
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

    // --- Category CRUD ---

    public List<Category> GetCategories() =>
        _db.Table<Category>().OrderBy(c => c.Name).ToList();

    public Category? GetCategory(int id) => _db.Find<Category>(id);

    public int SaveCategory(Category category) =>
        category.Id == 0 ? _db.Insert(category) : _db.Update(category);

    public void DeleteCategory(int id)
    {
        // Same as DeleteSubject: no FK cascade in sqlite-net, so detach first.
        var affectedTasks = _db.Table<TaskItem>().Where(t => t.CategoryId == id).ToList();
        foreach (var task in affectedTasks)
        {
            task.CategoryId = null;
            _db.Update(task);
        }

        _db.Delete<Category>(id);
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

    // --- Semester CRUD ---

    public List<Semester> GetSemesters() =>
        _db.Table<Semester>().OrderBy(s => s.StartDate).ToList();

    public Semester? GetSemester(int id) => _db.Find<Semester>(id);

    public int SaveSemester(Semester semester) =>
        semester.Id == 0 ? _db.Insert(semester) : _db.Update(semester);

    public void DeleteSemester(int id)
    {
        var semester = _db.Find<Semester>(id);

        foreach (var semesterBreak in GetBreaks(id))
            _db.Delete<SemesterBreak>(semesterBreak.Id);

        // No FK cascade. Detach the rules, but keep each series' horizon by
        // snapshotting the semester's end date rather than letting them all
        // silently become open-ended.
        var affectedRules = _db.Table<RecurrenceRule>().Where(r => r.SemesterId == id).ToList();
        foreach (var rule in affectedRules)
        {
            if (semester is not null && (rule.EndDate is null || rule.EndDate.Value.Date > semester.EndDate.Date))
                rule.EndDate = semester.EndDate.Date;

            rule.SemesterId = null;
            _db.Update(rule);
        }

        _db.Delete<Semester>(id);
    }

    public int CountRulesUsingSemester(int semesterId) =>
        _db.Table<RecurrenceRule>().Count(r => r.SemesterId == semesterId);

    // --- SemesterBreak CRUD ---

    public List<SemesterBreak> GetBreaks(int semesterId) =>
        _db.Table<SemesterBreak>().Where(b => b.SemesterId == semesterId).OrderBy(b => b.StartDate).ToList();

    public List<SemesterBreak> GetAllBreaks() => _db.Table<SemesterBreak>().ToList();

    public int SaveBreak(SemesterBreak semesterBreak) =>
        semesterBreak.Id == 0 ? _db.Insert(semesterBreak) : _db.Update(semesterBreak);

    public void DeleteBreak(int id) => _db.Delete<SemesterBreak>(id);

    public void Dispose() => _db.Close();
}
