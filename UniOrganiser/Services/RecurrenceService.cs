using UniOrganiser.Models;

namespace UniOrganiser.Services;

public class RecurrenceService(DatabaseService db)
{
    private const int MaterialisationWindowDays = 56;

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
        var windowEnd = today.AddDays(MaterialisationWindowDays);

        foreach (var rule in db.GetRules())
        {
            if (rule.EndDate.HasValue && rule.EndDate.Value.Date < today) continue;

            var occurrences = db.GetOccurrences(rule.Id);
            if (occurrences.Count == 0) continue;

            var template = occurrences[0];
            var existingDates = occurrences.Select(o => o.DueDate.Date).ToHashSet();

            foreach (var date in EnumerateDates(rule, today, windowEnd))
            {
                if (existingDates.Contains(date)) continue;

                db.SaveTask(new TaskItem
                {
                    Title = template.Title,
                    Description = template.Description,
                    SubjectId = template.SubjectId,
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

    public static IEnumerable<DateTime> EnumerateDates(RecurrenceRule rule, DateTime from, DateTime to)
    {
        var start = rule.StartDate.Date > from ? rule.StartDate.Date : from;
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
        RecurrenceFrequency newFrequency, string? newDaysOfWeekCsv, DateTime? newEndDate)
    {
        oldRule.EndDate = editedOccurrence.DueDate.AddDays(-1);
        db.SaveRule(oldRule);

        var newRule = new RecurrenceRule
        {
            Frequency = newFrequency,
            DaysOfWeekCsv = newDaysOfWeekCsv,
            StartDate = editedOccurrence.DueDate,
            EndDate = newEndDate,
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
