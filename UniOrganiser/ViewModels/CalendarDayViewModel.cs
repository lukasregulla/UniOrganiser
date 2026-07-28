namespace UniOrganiser.ViewModels;

public class CalendarDayViewModel(DateTime date, bool isCurrentMonth)
{
    public DateTime Date { get; } = date;
    public bool IsCurrentMonth { get; } = isCurrentMonth;
    public bool IsToday { get; } = date.Date == DateTime.Today;

    public List<TaskListItemViewModel> TasksOnDay { get; } = [];

    public IEnumerable<TaskListItemViewModel> VisibleTasks => TasksOnDay.Take(3);
    public int MoreCount => Math.Max(0, TasksOnDay.Count - 3);
    public bool HasMore => MoreCount > 0;
}
