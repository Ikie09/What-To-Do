using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using What_To_Do.Data;
using What_To_Do.Features.Push.Dtos;

namespace What_To_Do.Features.Push;

[ApiController]
[Route("api/push")]
[Authorize]
public class PushController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PushSender _sender;
    private readonly PushOptions _options;

    public PushController(AppDbContext db, PushSender sender, IOptions<PushOptions> options)
    {
        _db = db;
        _sender = sender;
        _options = options.Value;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // Only ever exposes the public key; the private key never leaves the server.
    [HttpGet("public-key")]
    public ActionResult<PushStatusResponse> GetPublicKey() => Ok(new PushStatusResponse
    {
        Enabled = _sender.Enabled,
        PublicKey = _sender.Enabled ? _sender.PublicKey : null
    });

    // Called whenever this device turns push on or changes its lead time; safe to repeat.
    [HttpPost("subscriptions")]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest dto)
    {
        if (!_sender.Enabled)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Push notifications are not configured on the server." });

        if (!PushEndpointValidator.IsAllowed(dto.Endpoint, _options.AllowedHostSuffixes))
            return BadRequest(new { message = "That push endpoint is not from a supported push service." });
        if (string.IsNullOrWhiteSpace(dto.P256dh) || dto.P256dh.Length > 200 ||
            string.IsNullOrWhiteSpace(dto.Auth) || dto.Auth.Length > 100)
            return BadRequest(new { message = "The subscription keys are missing or invalid." });

        var hash = Domain.Entities.PushSubscription.HashEndpoint(dto.Endpoint);
        var lead = Math.Clamp(dto.LeadMinutes, 1, 120);
        var zone = PushEndpointValidator.ResolveTimeZone(dto.TimeZone);
        var userAgent = Request.Headers.UserAgent.ToString();
        if (userAgent.Length > 300) userAgent = userAgent[..300];

        var existing = await _db.PushSubscriptions.FirstOrDefaultAsync(s => s.EndpointHash == hash);
        if (existing is null)
        {
            existing = new Domain.Entities.PushSubscription
            {
                Endpoint = dto.Endpoint,
                EndpointHash = hash,
                CreatedAt = DateTime.UtcNow
            };
            _db.PushSubscriptions.Add(existing);
        }

        // A browser can switch accounts; the device then belongs to whoever registered it last.
        existing.UserId = CurrentUserId;
        existing.P256dh = dto.P256dh;
        existing.Auth = dto.Auth;
        existing.LeadMinutes = lead;
        existing.TimeZone = zone;
        existing.UserAgent = userAgent;
        existing.FailureCount = 0;

        await _db.SaveChangesAsync();
        return Ok(new { id = existing.Id, leadMinutes = lead, timeZone = zone });
    }

    [HttpDelete("subscriptions")]
    public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeRequest dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Endpoint)) return BadRequest(new { message = "Endpoint is required." });

        var hash = Domain.Entities.PushSubscription.HashEndpoint(dto.Endpoint);
        await _db.PushSubscriptions
            .Where(s => s.EndpointHash == hash && s.UserId == CurrentUserId)
            .ExecuteDeleteAsync();

        return NoContent();
    }

    // "Send me a test" for every device the current user registered.
    [HttpPost("test")]
    public async Task<ActionResult<TestPushResponse>> SendTest(CancellationToken ct)
    {
        if (!_sender.Enabled)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Push notifications are not configured on the server." });

        var subscriptions = await _db.PushSubscriptions.Where(s => s.UserId == CurrentUserId).ToListAsync(ct);
        if (subscriptions.Count == 0) return NotFound(new { message = "No devices are registered for push yet." });

        var result = new TestPushResponse();
        var message = new PushMessageContent("Push notifications are on", "You'll get due-todo and meeting reminders here, even when the app is closed.", "test", "/settings");

        foreach (var subscription in subscriptions)
        {
            switch (await _sender.SendAsync(subscription, message, ct))
            {
                case PushResult.Sent:
                    result.Sent++;
                    break;
                case PushResult.Gone:
                    _db.PushSubscriptions.Remove(subscription);
                    result.Removed++;
                    break;
                default:
                    result.Failed++;
                    break;
            }
        }

        await _db.SaveChangesAsync(ct);
        return Ok(result);
    }
}
