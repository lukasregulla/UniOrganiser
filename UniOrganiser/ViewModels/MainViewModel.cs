using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniOrganiser.Services;

namespace UniOrganiser.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly TaskListViewModel _taskListViewModel;
    private readonly CalendarViewModel _calendarViewModel;
    private readonly SubjectsViewModel _subjectsViewModel;

    [ObservableProperty]
    private object? currentViewModel;

    [ObservableProperty]
    private bool isTasksActive = true;

    [ObservableProperty]
    private bool isCalendarActive;

    [ObservableProperty]
    private bool isSubjectsActive;

    public MainViewModel(DatabaseService db, RecurrenceService recurrenceService)
    {
        _taskListViewModel = new TaskListViewModel(db, recurrenceService);
        _calendarViewModel = new CalendarViewModel(db, recurrenceService);
        _subjectsViewModel = new SubjectsViewModel(db);

        CurrentViewModel = _taskListViewModel;
    }

    [RelayCommand]
    private void ShowTasks()
    {
        _taskListViewModel.Load();
        CurrentViewModel = _taskListViewModel;
        SetActiveTab(tasks: true, calendar: false, subjects: false);
    }

    [RelayCommand]
    private void ShowCalendar()
    {
        _calendarViewModel.LoadMonth();
        CurrentViewModel = _calendarViewModel;
        SetActiveTab(tasks: false, calendar: true, subjects: false);
    }

    [RelayCommand]
    private void ShowSubjects()
    {
        _subjectsViewModel.Load();
        CurrentViewModel = _subjectsViewModel;
        SetActiveTab(tasks: false, calendar: false, subjects: true);
    }

    private void SetActiveTab(bool tasks, bool calendar, bool subjects)
    {
        IsTasksActive = tasks;
        IsCalendarActive = calendar;
        IsSubjectsActive = subjects;
    }
}
