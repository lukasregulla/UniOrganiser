using SQLite;

namespace UniOrganiser.Models;

// Key-value store for the handful of one-off flags the app needs to remember
// across restarts (e.g. "default categories have been seeded").
public class AppSetting
{
    [PrimaryKey]
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
