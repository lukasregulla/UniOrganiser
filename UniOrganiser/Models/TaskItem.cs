using SQLite;

namespace UniOrganiser.Models;

public class TaskItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Indexed]
    public int? SubjectId { get; set; }

    [Indexed]
    public int? CategoryId { get; set; }

    [Indexed]
    public DateTime DueDate { get; set; }

    public TimeSpan? DueTime { get; set; }

    public bool IsCompleted { get; set; }

    public Priority Priority { get; set; } = Priority.Medium;

    [Indexed]
    public int? RecurrenceRuleId { get; set; }

    // Soft-delete for a single occurrence of a recurring task, so the
    // materialisation pass doesn't regenerate a date the user removed.
    public bool IsCancelledOccurrence { get; set; }
}
