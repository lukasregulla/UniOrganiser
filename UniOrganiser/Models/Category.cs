using SQLite;

namespace UniOrganiser.Models;

public class Category
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ColourHex { get; set; } = CategoryPalette.Colours[0];
}
