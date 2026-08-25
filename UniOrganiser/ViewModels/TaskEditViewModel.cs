using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniOrganiser.Models;
using UniOrganiser.Services;

namespace UniOrganiser.ViewModels;

public partial class TaskEditViewModel : ObservableObject
{
    private const int DefaultCustomRangeDays = 84;

    private readonly DatabaseService _db;
    private readonly RecurrenceService _recurrenceService;
    private readonly int _taskId;
    private readonly int? _originalRecurrenceRuleId;
    private readonly Dictionary<int, Semester> _semestersById;
    private readonly Dictionary<int, List<SemesterBreak>> _breaksBySemester;
    private bool _isInitialising = true;

    public event EventHandler<bool>? RequestClose;

    public List<PickerOption> SubjectOptions { get; }

    public List<PickerOption> CategoryOptions { get; }

    // Saved semesters, then a trailing null-Id "Custom range" entry. Note the
    // polarity is the opposite of the subject/category pickers, where the null
    // entry leads and means "none" - here the default should be a real semester.
    public List<PickerOption> RepeatRangeOptions { get; }

    public Priority[] PriorityOptions { get; } = Enum.GetValues<Priority>();

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
    private PickerOption? selectedSubjectOption;

    [ObservableProperty]
    private PickerOption? selectedCategoryOption;

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
    private PickerOption? selectedRepeatRangeOption;

    // Only meaningful in custom-range mode; a semester-bound rule takes its
    // horizon from the semester instead.
    [ObservableProperty]
    private DateTime? repeatEndDate;

    // True when editing an occurrence that already belongs to a recurrence rule -
    // controls whether the "apply to this occurrence / this and future" choice shows.
    [ObservableProperty]
    private bool isPartOfExistingSeries;

    [ObservableProperty]
    private bool applyToAllFuture;

    // Opt-in that promotes plain Weekly to WeeklyOnSpecificDays. Off means "same
    // weekday as the due date", which the null DaysOfWeekCsv anchor branch handles.
    [ObservableProperty]
    private bool repeatOnSpecificDays;

    // The frequency is picked with radio buttons rather than a dropdown: "Weekly" and
    // "weekly on specific days" read almost identically in a list, and landing on the
    // wrong one is silent. SelectedRepeatOption stays the single source of truth.
    public bool RepeatNone
    {
        get => SelectedRepeatOption == RepeatOption.None;
        set { if (value) SelectedRepeatOption = RepeatOption.None; }
    }

    public bool RepeatDaily
    {
        get => SelectedRepeatOption == RepeatOption.Daily;
        set { if (value) SelectedRepeatOption = RepeatOption.Daily; }
    }

    // Covers both weekly modes - RepeatOnSpecificDays chooses between them.
    public bool RepeatWeekly
    {
        get => SelectedRepeatOption is RepeatOption.Weekly or RepeatOption.WeeklyOnSpecificDays;
        set
        {
            if (value)
                SelectedRepeatOption = RepeatOnSpecificDays
                    ? RepeatOption.WeeklyOnSpecificDays
                    : RepeatOption.Weekly;
        }
    }

    public bool ShowDaysPicker => SelectedRepeatOption == RepeatOption.WeeklyOnSpecificDays;

    public bool ShowRepeatEndDate => SelectedRepeatOption != RepeatOption.None;

    public bool ShowCustomRange => ShowRepeatEndDate && SelectedRepeatRangeOption?.Id is null;

    public string? SelectedSemesterHint
    {
        get
        {
            if (SelectedRepeatRangeOption?.Id is not int semesterId) return null;
            if (!_semestersById.TryGetValue(semesterId, out var semester)) return null;

            var names = _breaksBySemester.TryGetValue(semesterId, out var breaks)
                ? breaks.OrderBy(b => b.StartDate).Select(b => b.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList()
                : [];

            var ends = $"Ends {semester.EndDate:d MMM yyyy}";
            return names.Count == 0 ? ends : $"{ends} · skips {string.Join(", ", names)}";
        }
    }

    partial void OnSelectedRepeatOptionChanged(RepeatOption value)
    {
        OnPropertyChanged(nameof(RepeatNone));
        OnPropertyChanged(nameof(RepeatDaily));
        OnPropertyChanged(nameof(RepeatWeekly));
        OnPropertyChanged(nameof(ShowDaysPicker));
        OnPropertyChanged(nameof(ShowRepeatEndDate));
        OnPropertyChanged(nameof(ShowCustomRange));
    }

    partial void OnRepeatOnSpecificDaysChanged(bool value)
    {
        if (!RepeatWeekly) return;

        SelectedRepeatOption = value ? RepeatOption.WeeklyOnSpecificDays : RepeatOption.Weekly;

        // Start from the weekday the user already chose, so switching the picker on
        // never lands on the "select at least one day" error with nothing ticked.
        if (value && DayToggles.All(d => !d.IsSelected))
            foreach (var toggle in DayToggles)
                toggle.IsSelected = toggle.Day == DueDate.DayOfWeek;
    }

    partial void OnSelectedRepeatRangeOptionChanged(PickerOption? value)
    {
        OnPropertyChanged(nameof(ShowCustomRange));
        OnPropertyChanged(nameof(SelectedSemesterHint));

        // Only prefill in response to the user switching to custom mode. Doing
        // it while loading would put an end date on an existing series that was
        // deliberately left open-ended.
        if (_isInitialising) return;

        if (value?.Id is null && RepeatEndDate is null)
            RepeatEndDate = DueDate.Date.AddDays(DefaultCustomRangeDays);
    }

    public TaskEditViewModel(DatabaseService db, RecurrenceService recurrenceService, TaskItem? existing,
        List<Subject> subjects, List<Category> categories)
    {
        _db = db;
        _recurrenceService = recurrenceService;

        SubjectOptions = [new PickerOption(null, "No subject", null)];
        SubjectOptions.AddRange(subjects.Select(s => new PickerOption(s.Id, s.Name, s.ColourHex)));

        CategoryOptions = [new PickerOption(null, "No category", null)];
        CategoryOptions.AddRange(categories.Select(c => new PickerOption(c.Id, c.Name, c.ColourHex)));

        var semesters = db.GetSemesters();
        _semestersById = semesters.ToDictionary(s => s.Id);
        _breaksBySemester = db.GetAllBreaks()
            .GroupBy(b => b.SemesterId)
            .ToDictionary(g => g.Key, g => g.ToList());

        RepeatRangeOptions = semesters
            .Select(s => new PickerOption(s.Id, $"{s.Name} ({s.StartDate:d MMM} – {s.EndDate:d MMM yyyy})", null))
            .ToList();
        RepeatRangeOptions.Add(new PickerOption(null, "Custom range…", null));

        if (existing is null)
        {
            _taskId = 0;
            SelectedSubjectOption = SubjectOptions[0];
            SelectedCategoryOption = CategoryOptions[0];
            SelectDefaultRepeatRange(semesters);
            _isInitialising = false;
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
        SelectedCategoryOption = CategoryOptions.FirstOrDefault(o => o.Id == existing.CategoryId) ?? CategoryOptions[0];

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
                RepeatOnSpecificDays = SelectedRepeatOption == RepeatOption.WeeklyOnSpecificDays;
                RepeatEndDate = rule.EndDate;
                SelectedRepeatRangeOption =
                    RepeatRangeOptions.FirstOrDefault(o => o.Id == rule.SemesterId)
                    ?? RepeatRangeOptions[^1];

                if (!string.IsNullOrWhiteSpace(rule.DaysOfWeekCsv))
                {
                    // Matches RecurrenceService.ParseDays, so a CSV that generates
                    // occurrences correctly also lights up the right toggles here.
                    var selected = rule.DaysOfWeekCsv
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    foreach (var toggle in DayToggles)
                        toggle.IsSelected = selected.Contains(toggle.Label);
                }
            }
        }

        // A one-off task the user might now turn into a series still needs a
        // sensible range preselected.
        if (SelectedRepeatRangeOption is null)
            SelectDefaultRepeatRange(semesters);

        _isInitialising = false;
    }

    // Auto-detect: the semester containing today, else the next one starting,
    // else a plain custom range carrying the old 12-week default.
    private void SelectDefaultRepeatRange(IReadOnlyList<Semester> semesters)
    {
        var current = SemesterCalendar.FindCurrentOrNext(DateTime.Today, semesters);

        if (current is null)
        {
            SelectedRepeatRangeOption = RepeatRangeOptions[^1];
            RepeatEndDate = DueDate.Date.AddDays(DefaultCustomRangeDays);
            return;
        }

        SelectedRepeatRangeOption = RepeatRangeOptions.First(o => o.Id == current.Id);
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

        var semesterId = SelectedRepeatOption == RepeatOption.None ? null : SelectedRepeatRangeOption?.Id;

        // The first occurrence is always materialised at the due date, and the
        // prune pass deliberately never removes it - so catch a due date that
        // doesn't belong to the chosen semester here rather than leaving one
        // stranded occurrence behind.
        if (semesterId is int chosenId && _semestersById.TryGetValue(chosenId, out var chosenSemester))
        {
            if (DueDate.Date < chosenSemester.StartDate.Date || DueDate.Date > chosenSemester.EndDate.Date)
            {
                ValidationError = $"The due date is outside {chosenSemester.Name} " +
                    $"({chosenSemester.StartDate:d MMM} – {chosenSemester.EndDate:d MMM yyyy}).";
                return;
            }

            var breaks = _breaksBySemester.TryGetValue(chosenId, out var found) ? found : [];
            if (SemesterCalendar.FindBreak(DueDate, breaks) is { } clashingBreak)
            {
                ValidationError = $"The due date falls in {clashingBreak.Name}.";
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
            CategoryId = SelectedCategoryOption?.Id,
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
                    StartDate = SeriesAnchor(task.DueDate, semesterId, daysCsv),
                    EndDate = semesterId is null ? RepeatEndDate : null,
                    SemesterId = semesterId,
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
                _recurrenceService.SplitRuleForFutureEdit(task, oldRule, frequency, daysCsv,
                    semesterId is null ? RepeatEndDate : null, semesterId);
            }
        }

        RequestClose?.Invoke(this, true);
    }

    // A semester-bound series covers the whole term, so it anchors at the semester start
    // rather than at whatever due date seeded it - materialisation treats
    // RecurrenceRule.StartDate as a hard floor. Splitting a series for a "this and all
    // future" edit deliberately anchors mid-term instead, which is why this only applies
    // to a series being created here.
    private DateTime SeriesAnchor(DateTime dueDate, int? semesterId, string? daysCsv)
    {
        if (semesterId is not int id || !_semestersById.TryGetValue(id, out var semester))
            return dueDate;

        var start = semester.StartDate.Date;
        if (SelectedRepeatOption == RepeatOption.Daily || !string.IsNullOrEmpty(daysCsv))
            return start;

        // Plain weekly carries its weekday in the anchor, so that has to survive the move back.
        return start.AddDays(((int)dueDate.DayOfWeek - (int)start.DayOfWeek + 7) % 7);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(this, false);
    }
}
