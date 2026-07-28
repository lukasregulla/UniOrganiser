using SQLite;

namespace UniOrganiser.Models;

public class Subject
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ColourHex { get; set; } = SubjectPalette.Colours[0];
}
