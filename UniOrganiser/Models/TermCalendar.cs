namespace UniOrganiser.Models;

public static class TermCalendar
{
    public static readonly DateTime TermStart = new(2026, 7, 27); // Week 1 Monday
    public const int TeachingWeeksBeforeBreak = 8;
    public static readonly DateTime BreakStart = new(2026, 9, 21); // Monday
    public const int BreakWeeks = 2;
    public const int TotalTeachingWeeks = 13;

    public static int? GetTeachingWeek(DateTime date)
    {
        date = date.Date;
        if (date < TermStart) return null;

        if (date < BreakStart)
            return (int)((date - TermStart).Days / 7) + 1;

        var breakEnd = BreakStart.AddDays(BreakWeeks * 7); // teaching resumes Monday 5 Oct 2026
        if (date < breakEnd) return null;

        var week = TeachingWeeksBeforeBreak + 1 + (int)((date - breakEnd).Days / 7);
        return week <= TotalTeachingWeeks ? week : null;
    }
}
