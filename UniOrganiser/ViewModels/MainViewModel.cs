using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniOrganiser.Services;

namespace UniOrganiser.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly TaskListViewModel _taskListViewModel;
    private readonly CalendarViewModel _calendarViewModel;
    private readonly TagsViewModel _tagsViewModel;
    private readonly SemestersViewModel _semestersViewModel;

    [ObservableProperty]
    private object? currentViewModel;

    [ObservableProperty]
    private bool isTasksActive = true;

    [ObservableProperty]
    private bool isCalendarActive;

    [ObservableProperty]
    private bool isTagsActive;

    [ObservableProperty]
    private bool isSemestersActive;

    public MainViewModel(DatabaseService db, RecurrenceService recurrenceService)
    {
        _taskListViewModel = new TaskListViewModel(db, recurrenceService);
        _calendarViewModel = new CalendarViewModel(db, recurrenceService);
        _tagsViewModel = new TagsViewModel(db);
        _semestersViewModel = new SemestersViewModel(db, recurrenceService);

        CurrentViewModel = _taskListViewModel;
    }

    [RelayCommand]
    private void ShowTasks()
    {
        _taskListViewModel.Load();
        CurrentViewModel = _taskListViewModel;
        SetActiveTab(tasks: true, calendar: false, tags: false, semesters: false);
    }

    [RelayCommand]
    private void ShowCalendar()
    {
        _calendarViewModel.LoadMonth();
        CurrentViewModel = _calendarViewModel;
        SetActiveTab(tasks: false, calendar: true, tags: false, semesters: false);
    }

    [RelayCommand]
    private void ShowTags()
    {
        _tagsViewModel.Load();
        CurrentViewModel = _tagsViewModel;
        SetActiveTab(tasks: false, calendar: false, tags: true, semesters: false);
    }

    [RelayCommand]
    private void ShowSemesters()
    {
        _semestersViewModel.Load();
        CurrentViewModel = _semestersViewModel;
        SetActiveTab(tasks: false, calendar: false, tags: false, semesters: true);
    }

    private void SetActiveTab(bool tasks, bool calendar, bool tags, bool semesters)
    {
        IsTasksActive = tasks;
        IsCalendarActive = calendar;
        IsTagsActive = tags;
        IsSemestersActive = semesters;
    }
}
