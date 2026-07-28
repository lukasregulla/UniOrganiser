# CLAUDE.md

Guidance for Claude Code when working in this repo.

## Project

**UniOrganiser** — a personal WPF desktop app: task list + calendar for uni organisation, including recurring weekly tasks (e.g. "listen to lecture", "weekly reading") per subject. Fully offline, single-user, local SQLite storage. Not a product, not multi-user, no auth, no cloud sync, no API calls at runtime.

## Stack

- .NET 10, C#, WPF
- MVVM via `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]` — don't hand-write `INotifyPropertyChanged` boilerplate)
- `sqlite-net-pcl` for local SQLite storage
- No third-party calendar/UI control libraries — the calendar grid is custom-built
- No web frameworks, no ASP.NET, no external services

## Project structure

```
/Models      -> TaskItem.cs, Subject.cs, Priority.cs, RecurrenceRule.cs, RecurrenceFrequency.cs
/ViewModels  -> one per View, suffixed ViewModel.cs
/Views       -> XAML views, suffixed View.xaml (dialogs suffixed Dialog.xaml)
/Services    -> DatabaseService.cs is the single source of truth for all SQLite reads/writes
/Themes      -> DarkTheme.xaml, resource dictionary for colours/styles
```

## Conventions

- **MVVM strictly**: Views contain no logic beyond trivial UI-only concerns (e.g. dialog open/close). All state and behaviour lives in ViewModels. Views and ViewModels communicate via data binding and commands, never direct references.
- **Navigation**: single `MainWindow`, view-switching via `ContentControl` bound to a "current ViewModel" property on `MainViewModel`. Avoid spawning extra windows except the task add/edit dialog.
- **Database**: all CRUD goes through `DatabaseService`. ViewModels never touch SQLite directly.
- **Naming**: `TaskItem` (not `Task`, to avoid clashing with `System.Threading.Tasks.Task`).
- **Nullable subject**: a `TaskItem` can have `SubjectId == null`, meaning "no subject" — handle this in both the UI (show as uncoloured/grey) and any queries.
- **Recurring tasks**: a `TaskItem` with a non-null `RecurrenceRuleId` is one occurrence of a repeating series. Occurrences are materialised as individual `TaskItem` rows (don't compute recurrence on the fly in the UI) — a background/startup routine should top up a rolling window of future occurrences (e.g. next 4–8 weeks) as time passes. Completing or editing one occurrence must never affect other occurrences in the series unless the user explicitly chooses "apply to all future occurrences".
- Favour straightforward, readable code over clever abstractions. Don't add interfaces/DI/extra layers unless there's an actual need.

## Style preferences

- No filler comments explaining obvious code
- Prefer explicit, descriptive names over abbreviations
- Keep XAML formatted with one attribute per line once a tag has more than ~3 attributes, for readability
- Dark theme colours and any other shared style values live in `Themes/DarkTheme.xaml` as resources — don't hardcode hex colours directly in view XAML
- All buttons need to be rounded
## Build order

Work incrementally in this order, and treat each step as a checkpoint rather than doing everything in one pass:

1. Scaffold project, NuGet packages, dark theme resource dictionary
2. `DatabaseService` + SQLite init, CRUD for `Subject` and `TaskItem`
3. `SubjectsView` (add/edit/delete, colour swatch picker)
4. `TaskListView` (list, add/edit dialog, complete/delete, subject colour tag)
5. Recurrence: `RecurrenceRule` CRUD, repeat option in task dialog, occurrence-generation logic
6. `CalendarView` (month grid, tasks per day including recurring occurrences, day click, edit from calendar)
7. Wire up `MainWindow` navigation
8. Polish: empty states, delete confirmation, validation (title + due date required)

## Commands

- Build: `dotnet build`
- Run: `dotnet run`
- No test suite currently — ask before assuming one should be added

## Out of scope (don't add unless explicitly asked)

- Cloud sync, accounts, multi-device support
- Push notifications/reminders/alarms (recurring tasks are in scope, notification popups are not)
- Grade/weighting tracking
- Pomodoro/study timers
- Any network calls