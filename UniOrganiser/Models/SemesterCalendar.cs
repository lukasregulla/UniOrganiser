namespace UniOrganiser.Models;

// Teaching-week labels derived from the saved semesters, replacing the old
// hardcoded TermCalendar. Break weeks don't count towards the week number, so
// numbering resumes where it left off once teaching restarts.
public static class SemesterCalendar
{
    public const string OutsideTerm = "Outside term";

    public static Semester? FindContaining(DateTime date, IEnumerable<Semester> semesters)
    {
        date = date.Date;
        return semesters.FirstOrDefault(s => date >= s.StartDate.Date && date <= s.EndDate.Date);
    }

    // The semester to default a new recurring task to: the one containing today,
    // otherwise the next one due to start.
    public static Semester? FindCurrentOrNext(DateTime today, IReadOnlyList<Semester> semesters) =>
        FindContaining(today, semesters)
        ?? semesters.Where(s => s.StartDate.Date > today.Date)
            .OrderBy(s => s.StartDate)
            .FirstOrDefault();

    public static bool IsInBreak(DateTime date, IEnumerable<SemesterBreak> breaks) =>
        FindBreak(date, breaks) is not null;

    public static SemesterBreak? FindBreak(DateTime date, IEnumerable<SemesterBreak> breaks)
    {
        date = date.Date;
        return breaks.FirstOrDefault(b => date >= b.StartDate.Date && date <= b.EndDate.Date);
    }

    public static string WeekLabel(DateTime date, IReadOnlyList<Semester> semesters,
        IReadOnlyDictionary<int, List<SemesterBreak>> breaksBySemester)
    {
        var semester = FindContaining(date, semesters);
        if (semester is null) return OutsideTerm;

        var breaks = breaksBySemester.TryGetValue(semester.Id, out var found) ? found : [];

        if (FindBreak(date, breaks) is { } activeBreak)
            return string.IsNullOrWhiteSpace(activeBreak.Name) ? "Break" : activeBreak.Name;

        // Assumes breaks are whole weeks aligned to the semester's start weekday,
        // which is true of a real term timetable.
        var daysSinceStart = (date.Date - semester.StartDate.Date).Days;
        var breakDaysBefore = breaks
            .Where(b => b.EndDate.Date < date.Date)
            .Sum(b => (b.EndDate.Date - b.StartDate.Date).Days + 1);

        return $"Week {(daysSinceStart - breakDaysBefore) / 7 + 1}";
    }
}
