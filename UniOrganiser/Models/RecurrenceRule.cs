using SQLite;

namespace UniOrganiser.Models;

public class RecurrenceRule
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public RecurrenceFrequency Frequency { get; set; }

    // Comma-separated day abbreviations, e.g. "Mon,Wed,Fri". Null for Daily
    // or for a plain weekly rule (same weekday as StartDate).
    public string? DaysOfWeekCsv { get; set; }

    // Anchor date the recurrence pattern is enumerated from.
    public DateTime StartDate { get; set; }

    // Null means the rule repeats indefinitely (or, when SemesterId is set,
    // until the semester ends). Also used to truncate a series in place when
    // the user edits or deletes "this and all future occurrences", so it still
    // applies to semester-bound rules - the earlier of the two dates wins.
    public DateTime? EndDate { get; set; }

    // Non-null means the series is bound to a saved semester: generation stops
    // at that semester's end date and skips its break periods. Null means a
    // custom range governed by EndDate alone.
    [Indexed]
    public int? SemesterId { get; set; }
}
