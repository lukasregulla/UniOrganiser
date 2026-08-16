using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniOrganiser.Models;
using UniOrganiser.Services;
using UniOrganiser.Views;

namespace UniOrganiser.ViewModels;

public partial class CalendarViewModel : ObservableObject
{
    private readonly DatabaseService _db;
    private readonly RecurrenceService _recurrenceService;

    public ObservableCollection<CalendarDayViewModel> Days { get; } = [];

    [ObservableProperty]
    private DateTime displayedMonth;

    [ObservableProperty]
    private CalendarDayViewModel? selectedDay;

    public string MonthLabel => DisplayedMonth.ToString("MMMM yyyy");

    partial void OnDisplayedMonthChanged(DateTime value) => OnPropertyChanged(nameof(MonthLabel));

    public CalendarViewModel(DatabaseService db, RecurrenceService recurrenceService)
    {
        _db = db;
        _recurrenceService = recurrenceService;
        DisplayedMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        LoadMonth();
    }

    public void LoadMonth()
    {
        var firstOfMonth = DisplayedMonth;
        var gridStart = firstOfMonth.AddDays(-((int)firstOfMonth.DayOfWeek + 6) % 7);
        var gridEnd = gridStart.AddDays(41);

        var subjects = _db.GetSubjects().ToDictionary(s => s.Id);
        var categories = _db.GetCategories().ToDictionary(c => c.Id);
        var semesters = _db.GetSemesters();
        var breaksBySemester = _db.GetAllBreaks()
            .GroupBy(b => b.SemesterId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var tasksByDate = _db.GetTasksInRange(gridStart, gridEnd)
            .Where(t => !t.IsCancelledOccurrence)
            .GroupBy(t => t.DueDate.Date)
            .ToDictionary(g => g.Key, g => g.OrderBy(t => t.DueTime ?? TimeSpan.Zero).ToList());

        var previouslySelectedDate = SelectedDay?.Date;

        Days.Clear();
        for (var d = gridStart; d <= gridEnd; d = d.AddDays(1))
        {
            var day = new CalendarDayViewModel(d, d.Month == firstOfMonth.Month);
            if (tasksByDate.TryGetValue(d.Date, out var dayTasks))
            {
                foreach (var task in dayTasks)
                {
                    subjects.TryGetValue(task.SubjectId ?? -1, out var subject);
                    categories.TryGetValue(task.CategoryId ?? -1, out var category);
                    var weekLabel = SemesterCalendar.WeekLabel(task.DueDate, semesters, breaksBySemester);
                    day.TasksOnDay.Add(new TaskListItemViewModel(task, subject, category, weekLabel));
                }
            }
            Days.Add(day);
        }

        SelectedDay = (previouslySelectedDate.HasValue ? Days.FirstOrDefault(d => d.Date == previouslySelectedDate.Value) : null)
            ?? Days.FirstOrDefault(d => d.Date == DateTime.Today)
            ?? Days.FirstOrDefault();
    }

    [RelayCommand]
    private void PreviousMonth()
    {
        DisplayedMonth = DisplayedMonth.AddMonths(-1);
        LoadMonth();
    }

    [RelayCommand]
    private void NextMonth()
    {
        DisplayedMonth = DisplayedMonth.AddMonths(1);
        LoadMonth();
    }

    [RelayCommand]
    private void GoToToday()
    {
        DisplayedMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        LoadMonth();
    }

    [RelayCommand]
    private void SelectDay(CalendarDayViewModel day)
    {
        SelectedDay = day;
    }

    [RelayCommand]
    private void AddTask()
    {
        var vm = new TaskEditViewModel(_db, _recurrenceService, null, _db.GetSubjects(), _db.GetCategories());
        if (SelectedDay is not null) vm.DueDate = SelectedDay.Date;
        var dialog = new TaskEditDialog(vm) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() == true) LoadMonth();
    }

    [RelayCommand]
    private void EditTask(TaskListItemViewModel item)
    {
        var vm = new TaskEditViewModel(_db, _recurrenceService, item.Task, _db.GetSubjects(), _db.GetCategories());
        var dialog = new TaskEditDialog(vm) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() == true) LoadMonth();
    }

    [RelayCommand]
    private void DeleteTask(TaskListItemViewModel item)
    {
        if (item.Task.RecurrenceRuleId is int)
        {
            var choice = MessageBox.Show(
                $"\"{item.Task.Title}\" is part of a recurring series.\n\n" +
                "Yes = delete this and all future occurrences\n" +
                "No = delete only this occurrence\n" +
                "Cancel = don't delete",
                "Delete Recurring Task",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            switch (choice)
            {
                case MessageBoxResult.Yes:
                    _recurrenceService.DeleteFutureOccurrences(item.Task);
                    break;
                case MessageBoxResult.No:
                    _recurrenceService.DeleteSingleOccurrence(item.Task);
                    break;
                default:
                    return;
            }
        }
        else
        {
            var result = MessageBox.Show(
                $"Delete task \"{item.Task.Title}\"?",
                "Delete Task",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;
            _db.DeleteTask(item.Task.Id);
        }

        LoadMonth();
    }
}
