using SQLite;

namespace UniOrganiser.Models;

// A non-teaching period inside a semester - recurring tasks skip these dates.
public class SemesterBreak
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int SemesterId { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }

    // Inclusive.
    public DateTime EndDate { get; set; }
}
