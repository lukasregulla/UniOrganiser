using UniOrganiser.Models;

namespace UniOrganiser.Services;

public class RecurrenceService(DatabaseService db)
{
    // Horizon for rules that aren't bound to a semester. Semester-bound rules
    // materialise their whole semester instead, start to end - that's bounded
    // by definition.
    private const int MaterialisationWindowDays = 84;

    private static readonly Dictionary<string, DayOfWeek> DayAbbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Mon"] = DayOfWeek.Monday,
        ["Tue"] = DayOfWeek.Tuesday,
        ["Wed"] = DayOfWeek.Wednesday,
        ["Thu"] = DayOfWeek.Thursday,
        ["Fri"] = DayOfWeek.Friday,
        ["Sat"] = DayOfWeek.Saturday,
        ["Sun"] = DayOfWeek.Sunday,
    };

    public void MaterialiseAll()
    {
        var today = DateTime.Today;
        var rollingEnd = today.AddDays(MaterialisationWindowDays);
        var semesters = db.GetSemesters().ToDictionary(s => s.Id);
        var breaksBySemester = db.GetAllBreaks()
            .GroupBy(b => b.SemesterId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var rule in db.GetRules())
        {
            // A dangling SemesterId (semester deleted out from under the rule)
            // degrades to the rolling window rather than dropping the series.
            var semester = rule.SemesterId is int semesterId && semesters.TryGetValue(semesterId, out var found)
                ? found
                : null;

            // Where the series genuinely ends, versus how far ahead we bother
            // generating. For a semester-bound rule they coincide; for anything
            // else the rolling window is only a generation horizon, so pruning
            // against it would delete perfectly valid future occurrences.
            var seriesEnd = MinDate(semester?.EndDate.Date, rule.EndDate?.Date) ?? DateTime.MaxValue.Date;
            var horizon = semester?.EndDate.Date ?? rollingEnd;
            var generationEnd = seriesEnd < horizon ? seriesEnd : horizon;

            // A semester-bound rule generates across its whole range rather than from
            // today forward, so a series seeded part-way through the term has no gap
            // behind it. Anything else keeps the rolling window.
            var generationStart = semester?.StartDate.Date ?? today;

            var breaks = semester is not null && breaksBySemester.TryGetValue(semester.Id, out var semesterBreaks)
                ? semesterBreaks
                : null;

            var occurrences = db.GetOccurrences(rule.Id);
            if (occurrences.Count == 0) continue;

            PruneStaleOccurrences(occurrences, today, seriesEnd, breaks);

            if (generationEnd < generationStart) continue;

            var template = occurrences[0];
            var existingDates = occurrences.Select(o => o.DueDate.Date).ToHashSet();

            foreach (var date in EnumerateDates(rule, generationStart, generationEnd, breaks))
            {
                if (existingDates.Contains(date)) continue;

                db.SaveTask(new TaskItem
                {
                    Title = template.Title,
                    Description = template.Description,
                    SubjectId = template.SubjectId,
                    CategoryId = template.CategoryId,
                    DueDate = date,
                    DueTime = template.DueTime,
                    IsCompleted = false,
                    Priority = template.Priority,
                    RecurrenceRuleId = rule.Id,
                    IsCancelledOccurrence = false,
                });
            }
        }
    }

    // Materialisation only ever adds, so attaching a semester to an existing
    // series, adding a break, or pulling a semester's end date in can strand
    // rows that no longer belong. Drop those, but never the earliest one:
    // MaterialiseAll reads its template off occurrences[0] and abandons any
    // rule that has none left, which would kill the series permanently.
    private static DateTime? MinDate(DateTime? a, DateTime? b)
    {
        if (a is null) return b;
        if (b is null) return a;
        return a.Value < b.Value ? a : b;
    }

    private void PruneStaleOccurrences(List<TaskItem> occurrences, DateTime today, DateTime seriesEnd,
        IReadOnlyList<SemesterBreak>? breaks)
    {
        for (var i = 1; i < occurrences.Count; i++)
        {
            var occurrence = occurrences[i];
            if (occurrence.DueDate.Date <= today) continue;
            if (occurrence.IsCompleted || occurrence.IsCancelledOccurrence) continue;

            var stale = occurrence.DueDate.Date > seriesEnd
                || (breaks is not null && FallsInBreak(occurrence.DueDate.Date, breaks));
            if (!stale) continue;

            db.DeleteTask(occurrence.Id);
            occurrences.RemoveAt(i);
            i--;
        }
    }

    private static bool FallsInBreak(DateTime date, IReadOnlyList<SemesterBreak> breaks) =>
        breaks.Any(b => date >= b.StartDate.Date && date <= b.EndDate.Date);

    // Break dates are skipped, not shifted: a weekly task simply has no
    // occurrence that week and the series still ends where it would have.
    public static IEnumerable<DateTime> EnumerateDates(RecurrenceRule rule, DateTime from, DateTime to,
        IReadOnlyList<SemesterBreak>? breaks = null, bool ignoreRuleStart = false)
    {
        foreach (var date in EnumeratePattern(rule, from, to, ignoreRuleStart))
        {
            if (breaks is not null && FallsInBreak(date, breaks)) continue;
            yield return date;
        }
    }

    private static IEnumerable<DateTime> EnumeratePattern(RecurrenceRule rule, DateTime from, DateTime to, bool ignoreRuleStart = false)
    {
        var start = !ignoreRuleStart && rule.StartDate.Date > from ? rule.StartDate.Date : from;
        var end = rule.EndDate.HasValue && rule.EndDate.Value.Date < to ? rule.EndDate.Value.Date : to;
        if (start > end) yield break;

        switch (rule.Frequency)
        {
            case RecurrenceFrequency.Daily:
                for (var d = start; d <= end; d = d.AddDays(1))
                    yield return d;
                break;

            case RecurrenceFrequency.Weekly when string.IsNullOrWhiteSpace(rule.DaysOfWeekCsv):
                var anchor = rule.StartDate.Date;
                while (anchor > start) anchor = anchor.AddDays(-7);
                while (anchor < start) anchor = anchor.AddDays(7);
                for (var d = anchor; d <= end; d = d.AddDays(7))
                    yield return d;
                break;

            case RecurrenceFrequency.Weekly:
                var days = ParseDays(rule.DaysOfWeekCsv!);
                for (var d = start; d <= end; d = d.AddDays(1))
                    if (days.Contains(d.DayOfWeek))
                        yield return d;
                break;
        }
    }

    private static HashSet<DayOfWeek> ParseDays(string csv) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(DayAbbreviations.ContainsKey)
            .Select(s => DayAbbreviations[s])
            .ToHashSet();

    // "This and all future occurrences" edit: end the old rule the day before the
    // edited occurrence, spin up a new rule from that date with the new pattern,
    // and re-point the edited row (and future ones) at it.
    public void SplitRuleForFutureEdit(TaskItem editedOccurrence, RecurrenceRule oldRule,
        RecurrenceFrequency newFrequency, string? newDaysOfWeekCsv, DateTime? newEndDate, int? newSemesterId)
    {
        oldRule.EndDate = editedOccurrence.DueDate.AddDays(-1);
        db.SaveRule(oldRule);

        var newRule = new RecurrenceRule
        {
            Frequency = newFrequency,
            DaysOfWeekCsv = newDaysOfWeekCsv,
            StartDate = editedOccurrence.DueDate,
            EndDate = newEndDate,
            SemesterId = newSemesterId,
        };
        db.SaveRule(newRule);

        var futureUnderOldRule = db.GetOccurrences(oldRule.Id)
            .Where(t => t.Id != editedOccurrence.Id && t.DueDate.Date > editedOccurrence.DueDate.Date)
            .ToList();
        foreach (var t in futureUnderOldRule) db.DeleteTask(t.Id);

        editedOccurrence.RecurrenceRuleId = newRule.Id;
        db.SaveTask(editedOccurrence);

        MaterialiseAll();
    }

    // Editing "this and all future" with repeat turned off: end the series here
    // and detach this occurrence into a plain one-off task.
    public void EndSeriesAtOccurrence(TaskItem occurrence, RecurrenceRule rule)
    {
        rule.EndDate = occurrence.DueDate.AddDays(-1);
        db.SaveRule(rule);

        var future = db.GetOccurrences(rule.Id)
            .Where(t => t.Id != occurrence.Id && t.DueDate.Date > occurrence.DueDate.Date)
            .ToList();
        foreach (var t in future) db.DeleteTask(t.Id);

        occurrence.RecurrenceRuleId = null;
        db.SaveTask(occurrence);
    }

    // Delete "this occurrence only": soft-delete so materialisation won't regenerate it.
    public void DeleteSingleOccurrence(TaskItem occurrence)
    {
        occurrence.IsCancelledOccurrence = true;
        db.SaveTask(occurrence);
    }

    // Delete "this and all future occurrences".
    public void DeleteFutureOccurrences(TaskItem occurrence)
    {
        if (occurrence.RecurrenceRuleId is not int ruleId)
        {
            db.DeleteTask(occurrence.Id);
            return;
        }

        var rule = db.GetRule(ruleId);
        if (rule is not null)
        {
            rule.EndDate = occurrence.DueDate.AddDays(-1);
            db.SaveRule(rule);
        }

        var future = db.GetOccurrences(ruleId)
            .Where(t => t.DueDate.Date >= occurrence.DueDate.Date)
            .ToList();
        foreach (var t in future) db.DeleteTask(t.Id);
    }
}
