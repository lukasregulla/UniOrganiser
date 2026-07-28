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

    // Null means the rule repeats indefinitely.
    public DateTime? EndDate { get; set; }
}
