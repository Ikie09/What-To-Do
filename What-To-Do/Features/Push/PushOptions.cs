namespace What_To_Do.Features.Push;

// Bound from the "Push" configuration section. The VAPID keys and subject belong in user-secrets.
public class PushOptions
{
    public string VapidPublicKey { get; set; } = string.Empty;
    public string VapidPrivateKey { get; set; } = string.Empty;
    // e.g. "mailto:you@example.com" — push services use it to contact the sender if something is wrong.
    public string Subject { get; set; } = string.Empty;

    // Subscription endpoints are user-supplied URLs the server will POST to, so only real push
    // services are accepted (suffix match on the host).
    public string[] AllowedHostSuffixes { get; set; } = [];

    public int ReminderIntervalSeconds { get; set; } = 60;
    // Local hour at which due-date reminders start for a day.
    public int MorningHour { get; set; } = 8;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(VapidPublicKey) &&
        !string.IsNullOrWhiteSpace(VapidPrivateKey) &&
        !string.IsNullOrWhiteSpace(Subject);
}
