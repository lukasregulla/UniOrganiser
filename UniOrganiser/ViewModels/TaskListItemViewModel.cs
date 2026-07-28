using UniOrganiser.Models;

namespace UniOrganiser.ViewModels;

public class TaskListItemViewModel(TaskItem task, Subject? subject)
{
    public TaskItem Task { get; } = task;
    public string? SubjectName { get; } = subject?.Name;
    public string SubjectColourHex { get; } = subject?.ColourHex ?? "#5A5A5A";
    public bool IsRecurring => Task.RecurrenceRuleId is not null;
}
