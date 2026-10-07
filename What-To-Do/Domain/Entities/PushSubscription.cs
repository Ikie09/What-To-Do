using System.Security.Cryptography;
using System.Text;

namespace What_To_Do.Domain.Entities;

// One browser/device that agreed to receive push notifications. Per device, so it is not part of /api/sync.
public class PushSubscription
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public AppUser? User { get; set; }

    public string Endpoint { get; set; } = string.Empty;
    // Endpoints are long, so uniqueness is enforced on this hash instead of the URL itself.
    public string EndpointHash { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;

    public int LeadMinutes { get; set; } = 10;
    // IANA time zone of the device, used to decide when "morning" is for due-date reminders.
    public string TimeZone { get; set; } = "UTC";
    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int FailureCount { get; set; }

    public static string HashEndpoint(string endpoint) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint))).ToLowerInvariant();
}
