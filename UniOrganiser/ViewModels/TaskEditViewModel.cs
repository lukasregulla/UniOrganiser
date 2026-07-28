using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniOrganiser.Models;
using UniOrganiser.Services;

namespace UniOrganiser.ViewModels;

public partial class TaskEditViewModel : ObservableObject
{
    private readonly DatabaseService _db;
    private readonly RecurrenceService _recurrenceService;
    private readonly int _taskId;
    private readonly int? _originalRecurrenceRuleId;

    public event EventHandler<bool>? RequestClose;

    public List<SubjectOption> SubjectOptions { get; }

    public Priority[] PriorityOptions { get; } = Enum.GetValues<Priority>();

    public RepeatOption[] RepeatOptions { get; } = Enum.GetValues<RepeatOption>();

    public List<DayToggle> DayToggles { get; } =
    [
        new("Mon", DayOfWeek.Monday),
        new("Tue", DayOfWeek.Tuesday),
        new("Wed", DayOfWeek.Wednesday),
        new("Thu", DayOfWeek.Thursday),
        new("Fri", DayOfWeek.Friday),
        new("Sat", DayOfWeek.Saturday),
        new("Sun", DayOfWeek.Sunday),
    ];

    [ObservableProperty]
    private string dialogTitle = "New Task";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string title = string.Empty;

    [ObservableProperty]
    private string? description;

    [ObservableProperty]
    private SubjectOption? selectedSubjectOption;

    [ObservableProperty]
    private DateTime dueDate = DateTime.Today;

    [ObservableProperty]
    private string dueTimeText = string.Empty;

    [ObservableProperty]
    private bool isCompleted;

    [ObservableProperty]
    private Priority priority = Priority.Medium;

    [ObservableProperty]
    private string? validationError;

    [ObservableProperty]
    private RepeatOption selectedRepeatOption = RepeatOption.None;

    [ObservableProperty]
    private DateTime? repeatEndDate;

    // True when editing an occurrence that already belongs to a recurrence rule -
    // controls whether the "apply to this occurrence / this and future" choice shows.
    [ObservableProperty]
    private bool isPartOfExistingSeries;

    [ObservableProperty]
    private bool applyToAllFuture;

    public bool ShowDaysPicker => SelectedRepeatOption == RepeatOption.WeeklyOnSpecificDays;

    public bool ShowRepeatEndDate => SelectedRepeatOption != RepeatOption.None;

    partial void OnSelectedRepeatOptionChanged(RepeatOption value)
    {
        OnPropertyChanged(nameof(ShowDaysPicker));
        OnPropertyChanged(nameof(ShowRepeatEndDate));
    }

    public TaskEditViewModel(DatabaseService db, RecurrenceService recurrenceService, TaskItem? existing, List<Subject> subjects)
    {
        _db = db;
        _recurrenceService = recurrenceService;

        SubjectOptions = [new SubjectOption(null, "No subject", null)];
        SubjectOptions.AddRange(subjects.Select(s => new SubjectOption(s.Id, s.Name, s.ColourHex)));

        if (existing is null)
        {
            _taskId = 0;
            SelectedSubjectOption = SubjectOptions[0];
            return;
        }

        _taskId = existing.Id;
        DialogTitle = "Edit Task";
        Title = existing.Title;
        Description = existing.Description;
        DueDate = existing.DueDate;
        DueTimeText = existing.DueTime?.ToString(@"hh\:mm") ?? string.Empty;
        IsCompleted = existing.IsCompleted;
        Priority = existing.Priority;
        SelectedSubjectOption = SubjectOptions.FirstOrDefault(o => o.Id == existing.SubjectId) ?? SubjectOptions[0];

        if (existing.RecurrenceRuleId is int ruleId)
        {
            var rule = db.GetRule(ruleId);
            if (rule is not null)
            {
                _originalRecurrenceRuleId = ruleId;
                IsPartOfExistingSeries = true;

                SelectedRepeatOption = rule.Frequency switch
                {
                    RecurrenceFrequency.Daily => RepeatOption.Daily,
                    RecurrenceFrequency.Weekly when !string.IsNullOrWhiteSpace(rule.DaysOfWeekCsv) => RepeatOption.WeeklyOnSpecificDays,
                    _ => RepeatOption.Weekly,
                };
                RepeatEndDate = rule.EndDate;

                if (!string.IsNullOrWhiteSpace(rule.DaysOfWeekCsv))
                {
                    var selected = rule.DaysOfWeekCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
                    foreach (var toggle in DayToggles)
                        toggle.IsSelected = selected.Contains(toggle.Label);
                }
            }
        }
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(Title);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        TimeSpan? dueTime = null;
        if (!string.IsNullOrWhiteSpace(DueTimeText))
        {
            if (!TimeSpan.TryParseExact(DueTimeText.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var parsed))
            {
                ValidationError = "Due time must be in HH:mm format (e.g. 14:30).";
                return;
            }
            dueTime = parsed;
        }

        string? daysCsv = null;
        if (SelectedRepeatOption == RepeatOption.WeeklyOnSpecificDays)
        {
            daysCsv = string.Join(",", DayToggles.Where(d => d.IsSelected).Select(d => d.Label));
            if (string.IsNullOrEmpty(daysCsv))
            {
                ValidationError = "Select at least one day of the week.";
                return;
            }
        }

        var frequency = SelectedRepeatOption == RepeatOption.Daily ? RecurrenceFrequency.Daily : RecurrenceFrequency.Weekly;

        var task = new TaskItem
        {
            Id = _taskId,
            Title = Title.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            SubjectId = SelectedSubjectOption?.Id,
            DueDate = DueDate.Date,
            DueTime = dueTime,
            IsCompleted = IsCompleted,
            Priority = Priority,
        };

        if (!IsPartOfExistingSeries)
        {
            task.RecurrenceRuleId = null;
            _db.SaveTask(task);

            if (SelectedRepeatOption != RepeatOption.None)
            {
                var rule = new RecurrenceRule
                {
                    Frequency = frequency,
                    DaysOfWeekCsv = daysCsv,
                    StartDate = task.DueDate,
                    EndDate = RepeatEndDate,
                };
                _db.SaveRule(rule);
                task.RecurrenceRuleId = rule.Id;
                _db.SaveTask(task);
                _recurrenceService.MaterialiseAll();
            }
        }
        else
        {
            var oldRule = _db.GetRule(_originalRecurrenceRuleId!.Value);

            if (!ApplyToAllFuture || oldRule is null)
            {
                task.RecurrenceRuleId = _originalRecurrenceRuleId;
                _db.SaveTask(task);
            }
            else if (SelectedRepeatOption == RepeatOption.None)
            {
                task.RecurrenceRuleId = _originalRecurrenceRuleId;
                _db.SaveTask(task);
                _recurrenceService.EndSeriesAtOccurrence(task, oldRule);
            }
            else
            {
                task.RecurrenceRuleId = _originalRecurrenceRuleId;
                _db.SaveTask(task);
                _recurrenceService.SplitRuleForFutureEdit(task, oldRule, frequency, daysCsv, RepeatEndDate);
            }
        }

        RequestClose?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(this, false);
    }
}
