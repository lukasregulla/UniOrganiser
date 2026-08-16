using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniOrganiser.Models;
using UniOrganiser.Services;

namespace UniOrganiser.ViewModels;

// Semesters and their break periods. Both are edited through one overlay -
// they're the same shape (name + start + end), only the validation and the
// table written to differ.
public partial class SemestersViewModel : ObservableObject
{
    private enum EditorKind { Semester, Break }

    private readonly DatabaseService _db;
    private readonly RecurrenceService _recurrenceService;

    public ObservableCollection<SemesterRowViewModel> Semesters { get; } = [];

    [ObservableProperty]
    private bool isEditorOpen;

    [ObservableProperty]
    private string editorTitle = "New Semester";

    [ObservableProperty]
    private string namePlaceholder = "e.g. Session 2 2026";

    private EditorKind _editingKind = EditorKind.Semester;
    private int _editingId;
    private int _editingSemesterId;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string editingName = string.Empty;

    [ObservableProperty]
    private DateTime editingStartDate = DateTime.Today;

    [ObservableProperty]
    private DateTime editingEndDate = DateTime.Today;

    [ObservableProperty]
    private string? validationError;

    public SemestersViewModel(DatabaseService db, RecurrenceService recurrenceService)
    {
        _db = db;
        _recurrenceService = recurrenceService;
        Load();
    }

    public void Load()
    {
        var breaksBySemester = _db.GetAllBreaks()
            .GroupBy(b => b.SemesterId)
            .ToDictionary(g => g.Key, g => g.OrderBy(b => b.StartDate).AsEnumerable());

        Semesters.Clear();
        foreach (var semester in _db.GetSemesters())
        {
            var breaks = breaksBySemester.TryGetValue(semester.Id, out var found) ? found : [];
            Semesters.Add(new SemesterRowViewModel(semester, breaks));
        }
    }

    private void OpenEditor(EditorKind kind, string title, string placeholder, int id, int semesterId,
        string name, DateTime start, DateTime end)
    {
        _editingKind = kind;
        _editingId = id;
        _editingSemesterId = semesterId;
        EditorTitle = title;
        NamePlaceholder = placeholder;
        EditingName = name;
        EditingStartDate = start;
        EditingEndDate = end;
        ValidationError = null;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void AddSemester()
    {
        var start = DateTime.Today;
        OpenEditor(EditorKind.Semester, "New Semester", "e.g. Session 2 2026", 0, 0,
            string.Empty, start, start.AddDays(13 * 7 - 1));
    }

    [RelayCommand]
    private void EditSemester(SemesterRowViewModel row) =>
        OpenEditor(EditorKind.Semester, "Edit Semester", "e.g. Session 2 2026",
            row.Semester.Id, 0, row.Semester.Name, row.Semester.StartDate, row.Semester.EndDate);

    [RelayCommand]
    private void AddBreak(SemesterRowViewModel row)
    {
        var start = row.Semester.StartDate;
        OpenEditor(EditorKind.Break, $"New Break in {row.Semester.Name}", "e.g. Mid-semester break",
            0, row.Semester.Id, string.Empty, start, start.AddDays(13));
    }

    [RelayCommand]
    private void EditBreak(SemesterBreak semesterBreak) =>
        OpenEditor(EditorKind.Break, "Edit Break", "e.g. Mid-semester break",
            semesterBreak.Id, semesterBreak.SemesterId, semesterBreak.Name,
            semesterBreak.StartDate, semesterBreak.EndDate);

    private bool CanSave() => !string.IsNullOrWhiteSpace(EditingName);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        var name = EditingName.Trim();
        var start = EditingStartDate.Date;
        var end = EditingEndDate.Date;

        if (end < start)
        {
            ValidationError = "The end date must be on or after the start date.";
            return;
        }

        if (_editingKind == EditorKind.Semester)
        {
            // Overlapping semesters would make "the semester containing today"
            // ambiguous, and the auto-detect just takes the first match.
            var clash = _db.GetSemesters()
                .FirstOrDefault(s => s.Id != _editingId && start <= s.EndDate.Date && end >= s.StartDate.Date);
            if (clash is not null)
            {
                ValidationError = $"That range overlaps {clash.Name} " +
                    $"({SemesterRowViewModel.FormatRange(clash.StartDate, clash.EndDate)}).";
                return;
            }

            _db.SaveSemester(new Semester { Id = _editingId, Name = name, StartDate = start, EndDate = end });
        }
        else
        {
            var parent = _db.GetSemester(_editingSemesterId);
            if (parent is null)
            {
                ValidationError = "That semester no longer exists.";
                return;
            }

            if (start < parent.StartDate.Date || end > parent.EndDate.Date)
            {
                ValidationError = $"A break must fall inside {parent.Name} " +
                    $"({SemesterRowViewModel.FormatRange(parent.StartDate, parent.EndDate)}).";
                return;
            }

            _db.SaveBreak(new SemesterBreak
            {
                Id = _editingId,
                SemesterId = _editingSemesterId,
                Name = name,
                StartDate = start,
                EndDate = end,
            });
        }

        IsEditorOpen = false;
        RefreshOccurrences();
    }

    [RelayCommand]
    private void Cancel()
    {
        IsEditorOpen = false;
    }

    [RelayCommand]
    private void DeleteSemester(SemesterRowViewModel row)
    {
        var ruleCount = _db.CountRulesUsingSemester(row.Semester.Id);
        var seriesNote = ruleCount == 0
            ? string.Empty
            : $"\n\n{ruleCount} recurring task series will keep their current end date " +
              "but stop tracking this semester.";

        var result = MessageBox.Show(
            $"Delete semester \"{row.Semester.Name}\"? Its break periods will be deleted too.{seriesNote}",
            "Delete Semester",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        _db.DeleteSemester(row.Semester.Id);
        RefreshOccurrences();
    }

    [RelayCommand]
    private void DeleteBreak(SemesterBreak semesterBreak)
    {
        var result = MessageBox.Show(
            $"Delete break \"{semesterBreak.Name}\"? Recurring tasks will start generating occurrences during it again.",
            "Delete Break",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        _db.DeleteBreak(semesterBreak.Id);
        RefreshOccurrences();
    }

    // Semester and break dates are resolved at materialisation time, so any
    // edit here has to re-run generation or the calendar stays stale until
    // the next launch.
    private void RefreshOccurrences()
    {
        _recurrenceService.MaterialiseAll();
        Load();
    }
}
