using System.Collections.ObjectModel;
using UniOrganiser.Models;

namespace UniOrganiser.ViewModels;

// One semester card on the Semesters page, with its break periods nested inside.
public class SemesterRowViewModel(Semester semester, IEnumerable<SemesterBreak> breaks)
{
    public Semester Semester { get; } = semester;

    public ObservableCollection<SemesterBreak> Breaks { get; } = new(breaks);

    public string Name => Semester.Name;

    public string DateRangeLabel => FormatRange(Semester.StartDate, Semester.EndDate);

    public static string FormatRange(DateTime start, DateTime end) =>
        $"{start:d MMM yyyy} – {end:d MMM yyyy}";
}
