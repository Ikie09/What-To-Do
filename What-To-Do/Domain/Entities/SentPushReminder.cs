namespace What_To_Do.Domain.Entities;

// Remembers which reminder was already pushed to which device, so each one is sent exactly once.
public class SentPushReminder
{
    public int Id { get; set; }
    public int SubscriptionId { get; set; }
    public PushSubscription? Subscription { get; set; }
    public string Key { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
