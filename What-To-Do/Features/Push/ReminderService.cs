using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using What_To_Do.Data;
using What_To_Do.Domain.Entities;
using What_To_Do.Domain.Enums;

namespace What_To_Do.Features.Push;

// Runs inside the API process: every tick it works out which push reminders are due and sends them.
public class ReminderService : BackgroundService
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan SentRetention = TimeSpan.FromDays(14);

    private readonly IServiceScopeFactory _scopes;
    private readonly PushSender _sender;
    private readonly PushOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<ReminderService> _logger;
    private DateTime _lastCleanup = DateTime.MinValue;

    public ReminderService(
        IServiceScopeFactory scopes,
        PushSender sender,
        IOptions<PushOptions> options,
        TimeProvider time,
        ILogger<ReminderService> logger)
    {
        _scopes = scopes;
        _sender = sender;
        _options = options.Value;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_sender.Enabled)
        {
            _logger.LogInformation("Web Push is not configured (Push:VapidPublicKey/VapidPrivateKey/Subject); reminders will not be sent. Run with --generate-vapid-keys to create keys.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.ReminderIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Reminder run failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = _time.GetUtcNow().UtcDateTime;
        await CleanupAsync(db, now, ct);

        var subscriptions = await db.PushSubscriptions.ToListAsync(ct);
        if (subscriptions.Count == 0) return;

        var userIds = subscriptions.Select(s => s.UserId).Distinct().ToList();
        var subscriptionIds = subscriptions.Select(s => s.Id).ToList();
        var today = now.Date;

        // Bounded look-ahead/behind: due dates within a day or two of today, meetings starting within the longest lead time.
        var todos = await db.Todos.AsNoTracking()
            .Where(t => userIds.Contains(t.UserId) && t.Status != TodoStatus.Done && t.DueDate != null &&
                        t.DueDate >= today.AddDays(-2) && t.DueDate <= today.AddDays(2))
            .ToListAsync(ct);

        var longestLead = subscriptions.Max(s => s.LeadMinutes);
        var meetings = await db.Meetings.AsNoTracking()
            .Where(m => userIds.Contains(m.UserId) && m.StartsAt > now && m.StartsAt <= now.AddMinutes(longestLead))
            .ToListAsync(ct);

        var alreadySent = (await db.SentPushReminders.AsNoTracking()
                .Where(r => subscriptionIds.Contains(r.SubscriptionId) && r.SentAt > now - SentRetention)
                .Select(r => new { r.SubscriptionId, r.Key })
                .ToListAsync(ct))
            .Select(r => (r.SubscriptionId, r.Key))
            .ToHashSet();

        foreach (var subscription in subscriptions)
        {
            var userTodos = todos.Where(t => t.UserId == subscription.UserId);
            var userMeetings = meetings.Where(m => m.UserId == subscription.UserId);

            foreach (var reminder in ReminderPlanner.Plan(subscription, userTodos, userMeetings, now, _options.MorningHour))
            {
                if (alreadySent.Contains((subscription.Id, reminder.Key))) continue;

                var outcome = await SendOnceAsync(db, subscription, reminder, ct);
                if (outcome == PushResult.Gone) break; // the subscription row is gone; stop for this device
            }
        }
    }

    // Records the reminder first (unique index) so two overlapping runs can never both send it.
    private async Task<PushResult> SendOnceAsync(AppDbContext db, Domain.Entities.PushSubscription subscription, PlannedReminder reminder, CancellationToken ct)
    {
        var record = new SentPushReminder { SubscriptionId = subscription.Id, Key = reminder.Key, SentAt = _time.GetUtcNow().UtcDateTime };
        db.SentPushReminders.Add(record);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Someone else recorded it first (or the subscription was just removed): nothing to do.
            db.Entry(record).State = EntityState.Detached;
            return PushResult.Sent;
        }

        var result = await _sender.SendAsync(subscription, reminder.Message, ct);

        switch (result)
        {
            case PushResult.Gone:
                db.PushSubscriptions.Remove(subscription);
                await db.SaveChangesAsync(ct);
                break;

            case PushResult.Failed:
                // Allow a retry on the next tick, but give up on devices that keep failing.
                db.SentPushReminders.Remove(record);
                subscription.FailureCount++;
                if (subscription.FailureCount >= MaxFailures) db.PushSubscriptions.Remove(subscription);
                await db.SaveChangesAsync(ct);
                break;

            default:
                if (subscription.FailureCount != 0)
                {
                    subscription.FailureCount = 0;
                    await db.SaveChangesAsync(ct);
                }
                break;
        }

        return result;
    }

    private async Task CleanupAsync(AppDbContext db, DateTime now, CancellationToken ct)
    {
        if (now - _lastCleanup < TimeSpan.FromHours(1)) return;
        _lastCleanup = now;

        var cutoff = now - SentRetention;
        await db.SentPushReminders.Where(r => r.SentAt < cutoff).ExecuteDeleteAsync(ct);
    }
}
