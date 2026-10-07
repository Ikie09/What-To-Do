using System.Net;
using System.Text.Json;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.Extensions.Options;

namespace What_To_Do.Features.Push;

public record PushMessageContent(string Title, string Body, string Tag, string Url);

public enum PushResult
{
    Sent,
    // 404/410: the browser revoked the subscription, so it should be forgotten.
    Gone,
    Failed
}

// Thin wrapper over Lib.Net.Http.WebPush: VAPID signing + aes128gcm payload encryption (the encoding
// every browser, including Safari/iOS, accepts). Singleton so one HttpClient is reused.
public class PushSender : IDisposable
{
    private const int TimeToLiveSeconds = 3600;

    private readonly PushOptions _options;
    private readonly ILogger<PushSender> _logger;
    private readonly PushServiceClient _client = new();
    private readonly VapidAuthentication? _vapid;

    public PushSender(IOptions<PushOptions> options, ILogger<PushSender> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (_options.IsConfigured)
        {
            _vapid = new VapidAuthentication(_options.VapidPublicKey, _options.VapidPrivateKey) { Subject = _options.Subject };
        }
    }

    public bool Enabled => _options.IsConfigured && _vapid is not null;
    public string PublicKey => _options.VapidPublicKey;

    public async Task<PushResult> SendAsync(Domain.Entities.PushSubscription subscription, PushMessageContent message, CancellationToken ct)
    {
        if (!Enabled) return PushResult.Failed;

        var payload = JsonSerializer.Serialize(new
        {
            title = message.Title,
            body = message.Body,
            tag = message.Tag,
            url = message.Url
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var target = new Lib.Net.Http.WebPush.PushSubscription { Endpoint = subscription.Endpoint };
        target.SetKey(PushEncryptionKeyName.P256DH, subscription.P256dh);
        target.SetKey(PushEncryptionKeyName.Auth, subscription.Auth);

        try
        {
            await _client.RequestPushMessageDeliveryAsync(
                target,
                new Lib.Net.Http.WebPush.PushMessage(payload) { TimeToLive = TimeToLiveSeconds },
                _vapid!,
                ct);
            return PushResult.Sent;
        }
        catch (PushServiceClientException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return PushResult.Gone;
        }
        catch (PushServiceClientException ex)
        {
            _logger.LogWarning("Push to subscription {Id} failed with {Status}", subscription.Id, ex.StatusCode);
            return PushResult.Failed;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or FormatException or ArgumentException)
        {
            // FormatException/ArgumentException: stored keys that are not valid base64url / P-256 points.
            _logger.LogWarning("Push to subscription {Id} failed: {Message}", subscription.Id, ex.Message);
            return PushResult.Failed;
        }
    }

    public void Dispose() => _vapid?.Dispose();
}
