namespace What_To_Do.Features.Push;

public static class PushEndpointValidator
{
    public const int MaxLength = 2000;

    // https only (plain http just for a loopback host that is explicitly allow-listed, which is how tests run),
    // and the host must belong to a known push service.
    public static bool IsAllowed(string? endpoint, IEnumerable<string> allowedSuffixes)
    {
        if (string.IsNullOrWhiteSpace(endpoint) || endpoint.Length > MaxLength) return false;
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)) return false;
        if (!string.IsNullOrEmpty(uri.UserInfo)) return false;

        var isLoopback = uri.IsLoopback;
        if (uri.Scheme != Uri.UriSchemeHttps && !(isLoopback && uri.Scheme == Uri.UriSchemeHttp)) return false;

        var host = uri.IdnHost.ToLowerInvariant();
        return allowedSuffixes.Any(s =>
        {
            var suffix = s.Trim().TrimStart('.').ToLowerInvariant();
            return suffix.Length > 0 && (host == suffix || host.EndsWith("." + suffix, StringComparison.Ordinal));
        });
    }

    public static string ResolveTimeZone(string? timeZone)
    {
        if (string.IsNullOrWhiteSpace(timeZone) || timeZone.Length > 64) return "UTC";
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZone);
            return timeZone;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return "UTC";
        }
    }
}
