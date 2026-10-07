using What_To_Do.Domain.Entities;

namespace What_To_Do.Features.Push;

public record PlannedReminder(string Key, PushMessageContent Message);

// Pure rules for "what should this device be told right now?" — no database, no clock of its own.
public static class ReminderPlanner
{
    public static IEnumerable<PlannedReminder> Plan(
        Domain.Entities.PushSubscription subscription,
        IEnumerable<Todo> todos,
        IEnumerable<Meeting> meetings,
        DateTime nowUtc,
        int morningHour)
    {
        var zone = ResolveZone(subscription.TimeZone);
        var local = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, zone);
        var today = local.Date;
        var morningHasStarted = local.TimeOfDay >= TimeSpan.FromHours(morningHour);

        // Due dates are date-only values (UTC midnight), so compare them with the device's calendar date.
        // Only "today" and "yesterday" are announced, so turning push on never floods old overdue items.
        if (morningHasStarted)
        {
            foreach (var todo in todos)
            {
                if (todo.DueDate is null || todo.IsDeleted || todo.Status == Domain.Enums.TodoStatus.Done) continue;

                var due = todo.DueDate.Value.Date;
                var day = due.ToString("yyyy-MM-dd");

                if (due == today)
                    yield return new PlannedReminder($"todo:{todo.Id}:{day}:due",
                        new PushMessageContent("Due today", todo.Title, $"todo-{todo.Id}", "/todos"));
                else if (due == today.AddDays(-1))
                    yield return new PlannedReminder($"todo:{todo.Id}:{day}:overdue",
                        new PushMessageContent("Overdue", todo.Title, $"todo-{todo.Id}", "/todos"));
            }
        }

        foreach (var meeting in meetings)
        {
            if (meeting.IsDeleted) continue;

            var minutes = (meeting.StartsAt - nowUtc).TotalMinutes;
            if (minutes <= 0 || minutes > subscription.LeadMinutes) continue;

            var rounded = Math.Max(1, (int)Math.Round(minutes));
            var body = string.IsNullOrWhiteSpace(meeting.Location) ? meeting.Title : $"{meeting.Title} · {meeting.Location}";
            yield return new PlannedReminder($"meeting:{meeting.Id}:{meeting.StartsAt:yyyyMMddHHmm}",
                new PushMessageContent($"Meeting in {rounded} min", body, $"meeting-{meeting.Id}", "/meetings"));
        }
    }

    private static TimeZoneInfo ResolveZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { return TimeZoneInfo.Utc; }
    }
}
