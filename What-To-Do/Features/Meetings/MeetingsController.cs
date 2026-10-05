using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using What_To_Do.Data;
using What_To_Do.Domain.Entities;
using What_To_Do.Features.Meetings.Dtos;

namespace What_To_Do.Features.Meetings;

[ApiController]
[Route("api/meetings")]
[Authorize]
public class MeetingsController : ControllerBase
{
    private readonly AppDbContext _db;
    public MeetingsController(AppDbContext db) => _db = db;

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MeetingResponse>>> GetAll(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? search)
    {
        var query = _db.Meetings
            .Where(m => m.UserId == CurrentUserId)
            .AsQueryable();

        if (from.HasValue) query = query.Where(m => m.EndsAt >= from.Value);
        if (to.HasValue) query = query.Where(m => m.StartsAt <= to.Value);

        if (!string.IsNullOrEmpty(search))
            query = query.Where(m => m.Title.Contains(search) ||
                                     m.Location != null && m.Location.Contains(search) ||
                                     m.MeetingNotes != null && m.MeetingNotes.Contains(search));

        var meetings = await query
            .OrderBy(m => m.StartsAt)
            .Select(m => MapToDto(m))
            .ToListAsync();

        return Ok(meetings);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MeetingResponse>> GetById(int id)
    {
        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.Id == id && m.UserId == CurrentUserId);
        if (meeting is null) return NotFound(new { message = $"Meeting with id {id} not found." });

        return Ok(MapToDto(meeting));
    }

    [HttpPost]
    public async Task<ActionResult<MeetingResponse>> Create([FromBody] CreateMeetingRequest dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(new { message = "Title is required." });
        if (dto.EndsAt < dto.StartsAt)
            return BadRequest(new { message = "End time must be after the start time." });

        var meeting = new Meeting
        {
            Title = dto.Title.Trim(),
            Description = dto.Description?.Trim(),
            Location = dto.Location?.Trim(),
            StartsAt = dto.StartsAt,
            EndsAt = dto.EndsAt,
            MeetingNotes = dto.MeetingNotes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            UserId = CurrentUserId
        };

        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = meeting.Id }, MapToDto(meeting));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<MeetingResponse>> Update(int id, [FromBody] UpdateMeetingRequest dto)
    {
        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.Id == id && m.UserId == CurrentUserId);
        if (meeting is null) return NotFound(new { message = $"Meeting with id {id} not found." });

        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(new { message = "Title is required." });
        if (dto.EndsAt < dto.StartsAt)
            return BadRequest(new { message = "End time must be after the start time." });

        meeting.Title = dto.Title.Trim();
        meeting.Description = dto.Description?.Trim();
        meeting.Location = dto.Location?.Trim();
        meeting.StartsAt = dto.StartsAt;
        meeting.EndsAt = dto.EndsAt;
        meeting.MeetingNotes = dto.MeetingNotes;
        meeting.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(MapToDto(meeting));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.Id == id && m.UserId == CurrentUserId);
        if (meeting is null) return NotFound(new { message = $"Meeting with id {id} not found." });

        meeting.IsDeleted = true;
        meeting.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private static MeetingResponse MapToDto(Meeting m) => new()
    {
        Id = m.Id,
        Title = m.Title,
        Description = m.Description,
        Location = m.Location,
        StartsAt = m.StartsAt,
        EndsAt = m.EndsAt,
        MeetingNotes = m.MeetingNotes,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt
    };
}
