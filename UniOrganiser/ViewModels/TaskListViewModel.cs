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
    public ObservableCollection<SubjectToggle> SubjectToggles { get; } = [];

    [ObservableProperty]
    private bool showCompleted;

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
        RefreshSubjectToggles(subjects.Values);

        var selectedSubjectIds = SubjectToggles.Where(t => t.IsSelected).Select(t => t.SubjectId).ToHashSet();

        var tasks = _db.GetTasks()
            .Where(t => !t.IsCancelledOccurrence)
            .Where(t => ShowCompleted || !t.IsCompleted)
            .Where(t => selectedSubjectIds.Count == 0 || selectedSubjectIds.Contains(t.SubjectId))
            .Where(t => t.RecurrenceRuleId == null
                || (t.DueDate >= startOfWeek && t.DueDate < endOfWeek)
                || (t.DueDate < startOfWeek && !t.IsCompleted))
            .OrderBy(t => t.IsCompleted)
            .ThenBy(t => t.DueDate)
            .ThenBy(t => t.DueTime ?? TimeSpan.MaxValue);

        Items.Clear();
        foreach (var task in tasks)
        {
            subjects.TryGetValue(task.SubjectId ?? -1, out var subject);
            Items.Add(new TaskListItemViewModel(task, subject));
        }
    }

    private void RefreshSubjectToggles(IEnumerable<Subject> subjects)
    {
        var previouslySelected = SubjectToggles.Where(t => t.IsSelected).Select(t => t.SubjectId).ToHashSet();

        foreach (var toggle in SubjectToggles)
            toggle.PropertyChanged -= OnSubjectToggleChanged;
        SubjectToggles.Clear();

        SubjectToggles.Add(new SubjectToggle(null, "No subject", null) { IsSelected = previouslySelected.Contains(null) });
        foreach (var subject in subjects.OrderBy(s => s.Name))
        {
            SubjectToggles.Add(new SubjectToggle(subject.Id, subject.Name, subject.ColourHex)
            {
                IsSelected = previouslySelected.Contains(subject.Id)
            });
        }

        foreach (var toggle in SubjectToggles)
            toggle.PropertyChanged += OnSubjectToggleChanged;
    }

    private void OnSubjectToggleChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SubjectToggle.IsSelected)) Load();
    }

    [RelayCommand]
    private void AddTask()
    {
        var vm = new TaskEditViewModel(_db, _recurrenceService, null, _db.GetSubjects());
        var dialog = new TaskEditDialog(vm) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() == true) Load();
    }

    [RelayCommand]
    private void EditTask(TaskListItemViewModel item)
    {
        var vm = new TaskEditViewModel(_db, _recurrenceService, item.Task, _db.GetSubjects());
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
