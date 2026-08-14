# CLAUDE.md

Guidance for Claude Code when working in this repo.

## Project

**UniOrganiser** — a personal WPF desktop app: task list + calendar for uni organisation, including recurring weekly tasks (e.g. "listen to lecture", "weekly reading") per subject. Fully offline, single-user, local SQLite storage. Not a product, not multi-user, no auth, no cloud sync, no API calls at runtime.

## Stack

- .NET 10, C#, WPF
- MVVM via `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]` — don't hand-write `INotifyPropertyChanged` boilerplate)
- `sqlite-net-pcl` for local SQLite storage
- `WPF-UI` (lepoco/wpfui) for Fluent Design styling — `ui:FluentWindow` shell, `ui:Button`/`ui:TextBox`/`ui:Card`/`ui:CardExpander`/`ui:ToggleSwitch`/`ui:SymbolIcon`, and its theme resource dictionaries. XAML namespace: `http://schemas.lepo.co/wpfui/2022/xaml`
- No third-party *calendar* control — the calendar grid is still custom-built (`ItemsControl` + `UniformGrid Columns="7"`)
- No web frameworks, no ASP.NET, no external services

## Project structure

```
/Models      -> TaskItem.cs, Subject.cs, Priority.cs, RecurrenceRule.cs, RecurrenceFrequency.cs
/ViewModels  -> one per View, suffixed ViewModel.cs
/Views       -> XAML views, suffixed View.xaml (dialogs suffixed Dialog.xaml)
/Services    -> DatabaseService.cs is the single source of truth for all SQLite reads/writes
/Converters  -> IValueConverter / IMultiValueConverter helpers, registered in App.xaml
```

There is no app-level theme dictionary. Colours and typography come from WPF-UI's
`ui:ThemesDictionary` + `ui:ControlsDictionary`, merged in `App.xaml`. Styles used by only one
view live in that view's own `Resources`.

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
- Never hardcode hex colours in view XAML. Use WPF-UI theme brush keys via **`DynamicResource`**, never `StaticResource` — `StaticResource` resolves once and won't follow a theme change. Common keys: `ApplicationBackgroundBrush`, `CardBackgroundFillColorDefaultBrush`, `CardStrokeColorDefaultBrush`, `ControlFillColorDefaultBrush`, `TextFillColorPrimaryBrush`, `TextFillColorSecondaryBrush`, `AccentFillColorDefaultBrush`, `SystemFillColorCriticalBrush`. The one exception is user-chosen subject colours, which are data and go through `HexToBrushConverter`.
- No hardcoded `FontSize`/`FontWeight`. Use WPF-UI typography styles: `CaptionTextBlockStyle`, `BodyTextBlockStyle`, `BodyStrongTextBlockStyle`, `SubtitleTextBlockStyle`, `TitleTextBlockStyle`.
- Icons are `ui:SymbolIcon` (Segoe Fluent Icons), never text glyphs or emoji
- Margins and padding in multiples of 8 (8/16/24)
- Corner radius comes from `{DynamicResource ControlCornerRadius}` / `{DynamicResource OverlayCornerRadius}` — don't hand-pick radii
- The app is dark-only (`ApplicationThemeManager.Apply(ApplicationTheme.Dark, ...)` in `App.xaml.cs`). The `DynamicResource` rule above still holds so a light mode stays cheap to add.
- Don't override WPF-UI's built-in hover/pressed states with custom triggers unless there's a real need

## Build order

Work incrementally in this order, and treat each step as a checkpoint rather than doing everything in one pass:

1. Scaffold project, NuGet packages, WPF-UI theme dictionaries in `App.xaml`
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