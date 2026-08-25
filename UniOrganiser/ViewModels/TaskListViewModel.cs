using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniOrganiser.Models;
using UniOrganiser.Services;
using UniOrganiser.Views;

namespace UniOrganiser.ViewModels;

public partial class TaskListViewModel : ObservableObject
{
    private readonly DatabaseService _db;
    private readonly RecurrenceService _recurrenceService;

    public ObservableCollection<TaskListItemViewModel> Items { get; } = [];
    public ObservableCollection<FilterToggle> SubjectToggles { get; } = [];
    public ObservableCollection<FilterToggle> CategoryToggles { get; } = [];

    [ObservableProperty]
    private bool showCompleted;

    // Drives which empty-state message shows: "nothing yet" vs "nothing matches".
    [ObservableProperty]
    private bool hasActiveFilters;

    public TaskListViewModel(DatabaseService db, RecurrenceService recurrenceService)
    {
        _db = db;
        _recurrenceService = recurrenceService;
        Load();
    }

    partial void OnShowCompletedChanged(bool value) => Load();

    public void Load()
    {
        var today = DateTime.Today;
        var startOfWeek = today.AddDays(-((int)today.DayOfWeek + 6) % 7); // Monday-start
        var endOfWeek = startOfWeek.AddDays(7); // exclusive

        var subjects = _db.GetSubjects().ToDictionary(s => s.Id);
        var categories = _db.GetCategories().ToDictionary(c => c.Id);
        var semesters = _db.GetSemesters();
        var breaksBySemester = _db.GetAllBreaks()
            .GroupBy(b => b.SemesterId)
            .ToDictionary(g => g.Key, g => g.ToList());
        RefreshToggles(SubjectToggles, "No subject",
            subjects.Values.OrderBy(s => s.Name).Select(s => (s.Id, s.Name, s.ColourHex)));
        RefreshToggles(CategoryToggles, "No category",
            categories.Values.OrderBy(c => c.Name).Select(c => (c.Id, c.Name, c.ColourHex)));

        var selectedSubjectIds = SubjectToggles.Where(t => t.IsSelected).Select(t => t.Id).ToHashSet();
        var selectedCategoryIds = CategoryToggles.Where(t => t.IsSelected).Select(t => t.Id).ToHashSet();
        HasActiveFilters = selectedSubjectIds.Count > 0 || selectedCategoryIds.Count > 0;

        var tasks = _db.GetTasks()
            .Where(t => !t.IsCancelledOccurrence)
            // "Show completed" is for finished one-off work; a term of ticked-off lectures
            // would bury it, so completed occurrences stay hidden either way.
            .Where(t => !t.IsCompleted || (ShowCompleted && t.RecurrenceRuleId == null))
            .Where(t => selectedSubjectIds.Count == 0 || selectedSubjectIds.Contains(t.SubjectId))
            .Where(t => selectedCategoryIds.Count == 0 || selectedCategoryIds.Contains(t.CategoryId))
            // Recurring occurrences run to the end of the current week and no further -
            // a whole semester of future lectures would swamp the list.
            .Where(t => t.RecurrenceRuleId == null || t.DueDate < endOfWeek)
            // Date first: the view groups by week, and a group is created wherever its
            // first item lands, so a completed-last sort would strand the older weeks.
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.DueTime ?? TimeSpan.MaxValue);

        Items.Clear();
        foreach (var task in tasks)
        {
            subjects.TryGetValue(task.SubjectId ?? -1, out var subject);
            categories.TryGetValue(task.CategoryId ?? -1, out var category);
            var weekLabel = SemesterCalendar.WeekLabel(task.DueDate, semesters, breaksBySemester);
            Items.Add(new TaskListItemViewModel(task, subject, category, weekLabel));
        }
    }

    private void RefreshToggles(ObservableCollection<FilterToggle> toggles, string noneLabel,
        IEnumerable<(int Id, string Name, string ColourHex)> items)
    {
        var previouslySelected = toggles.Where(t => t.IsSelected).Select(t => t.Id).ToHashSet();

        foreach (var toggle in toggles)
            toggle.PropertyChanged -= OnFilterToggleChanged;
        toggles.Clear();

        toggles.Add(new FilterToggle(null, noneLabel, null) { IsSelected = previouslySelected.Contains(null) });
        foreach (var (id, name, colourHex) in items)
            toggles.Add(new FilterToggle(id, name, colourHex) { IsSelected = previouslySelected.Contains(id) });

        foreach (var toggle in toggles)
            toggle.PropertyChanged += OnFilterToggleChanged;
    }

    private void OnFilterToggleChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FilterToggle.IsSelected)) Load();
    }

    [RelayCommand]
    private void AddTask()
    {
        var vm = new TaskEditViewModel(_db, _recurrenceService, null, _db.GetSubjects(), _db.GetCategories());
        var dialog = new TaskEditDialog(vm) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() == true) Load();
    }

    [RelayCommand]
    private void EditTask(TaskListItemViewModel item)
    {
        var vm = new TaskEditViewModel(_db, _recurrenceService, item.Task, _db.GetSubjects(), _db.GetCategories());
        var dialog = new TaskEditDialog(vm) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() == true) Load();
    }

    [RelayCommand]
    private void ToggleComplete(TaskListItemViewModel item)
    {
        item.Task.IsCompleted = !item.Task.IsCompleted;
        _db.SaveTask(item.Task);
        Load();
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

        Load();
    }
}
