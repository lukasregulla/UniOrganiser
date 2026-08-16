using SQLite;

namespace UniOrganiser.Models;

public class Semester
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }

    // Inclusive, matching RecurrenceRule.EndDate.
    public DateTime EndDate { get; set; }
}
