using UniOrganiser.Models;

namespace UniOrganiser.ViewModels;

public class TaskListItemViewModel(TaskItem task, Subject? subject, Category? category, string weekLabel)
{
    public TaskItem Task { get; } = task;
    public string? SubjectName { get; } = subject?.Name;
    public string SubjectColourHex { get; } = subject?.ColourHex ?? "#5A5A5A";
    public string? CategoryName { get; } = category?.Name;
    public string CategoryColourHex { get; } = category?.ColourHex ?? "#5A5A5A";
    public bool HasCategory => CategoryName is not null;
    public string TagTooltip => $"{CategoryName ?? "No category"} · {SubjectName ?? "No subject"}";
    public bool IsRecurring => Task.RecurrenceRuleId is not null;
    // Computed once per load by TaskListViewModel, which already has the
    // semesters and breaks to hand - no per-row lookup.
    public string WeekLabel { get; } = weekLabel;
}
