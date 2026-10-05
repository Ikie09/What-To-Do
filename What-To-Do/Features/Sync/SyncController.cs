using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using What_To_Do.Data;
using What_To_Do.Domain.Entities;
using What_To_Do.Domain.Enums;
using What_To_Do.Features.Sync.Dtos;

namespace What_To_Do.Features.Sync;
[ApiController]
[Route("api/sync")]
[Authorize]
public class SyncController : ControllerBase
{
    private readonly AppDbContext _db;
    public SyncController(AppDbContext db) => _db = db;

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<ActionResult<SyncResponse>> GetChanges([FromQuery] DateTime? since)
    {
        // Capture server time BEFORE querying, so nothing that changes
        // mid-request gets missed on the client's next sync call.
        var serverTime = DateTime.UtcNow;
        var cutoff = since ?? DateTime.MinValue;

        var todos = await _db.Todos
            .IgnoreQueryFilters()
            .Where(t => t.UserId == CurrentUserId && t.UpdatedAt > cutoff)
            .Select(t => new TodoSyncDto
            {
                Id = t.Id,
                Title = t.Title,
                Notes = t.Notes,
                IsCompleted = t.IsCompleted,
                Status = t.Status.ToString(),
                SortOrder = t.SortOrder,
                Priority = t.Priority.ToString(),
                DueDate = t.DueDate,
                CategoryId = t.CategoryId,
                UpdatedAt = t.UpdatedAt,
                IsDeleted = t.IsDeleted
            })
            .ToListAsync();

        var categories = await _db.Categories
            .IgnoreQueryFilters()
            .Where(c => (c.UserId == null || c.UserId == CurrentUserId) && c.UpdatedAt > cutoff)
            .Select(c => new CategorySyncDto
            {
                Id = c.Id,
                Name = c.Name,
                Color = c.Color,
                UpdatedAt = c.UpdatedAt,
                IsDeleted = c.IsDeleted
            })
            .ToListAsync();

        var notes = await _db.Notes
            .IgnoreQueryFilters()
            .Where(n => n.UserId == CurrentUserId && n.UpdatedAt > cutoff)
            .Select(n => new NoteSyncDto
            {
                Id = n.Id,
                Title = n.Title,
                Body = n.Body,
                UpdatedAt = n.UpdatedAt,
                IsDeleted = n.IsDeleted
            })
            .ToListAsync();

        var meetings = await _db.Meetings
            .IgnoreQueryFilters()
            .Where(m => m.UserId == CurrentUserId && m.UpdatedAt > cutoff)
            .Select(m => new MeetingSyncDto
            {
                Id = m.Id,
                Title = m.Title,
                Description = m.Description,
                Location = m.Location,
                StartsAt = m.StartsAt,
                EndsAt = m.EndsAt,
                MeetingNotes = m.MeetingNotes,
                UpdatedAt = m.UpdatedAt,
                IsDeleted = m.IsDeleted
            })
            .ToListAsync();

        return Ok(new SyncResponse
        {
            ServerTime = serverTime,
            Todos = todos,
            Categories = categories,
            Notes = notes,
            Meetings = meetings
        });
    }

    // Client pushes its offline queue of changes here
    [HttpPost("push")]
    public async Task<IActionResult> PushChanges([FromBody] SyncPushRequest request)
    {
        foreach (var change in request.Todos)
        {
            var existing = await _db.Todos
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == change.Id && t.UserId == CurrentUserId);

            // Last-write-wins: only apply if the incoming change is newer
            if (existing != null && change.UpdatedAt <= existing.UpdatedAt)
                continue;

            if (existing is null)
            {
                var created = new Todo
                {
                    Title = change.Title,
                    Notes = change.Notes,
                    SortOrder = change.SortOrder,
                    Priority = Enum.Parse<PriorityLevel>(change.Priority),
                    DueDate = change.DueDate,
                    CategoryId = change.CategoryId,
                    UpdatedAt = change.UpdatedAt,
                    IsDeleted = change.IsDeleted,
                    UserId = CurrentUserId,
                    CreatedAt = DateTime.UtcNow
                };
                created.SetStatusFrom(change.Status, change.IsCompleted);
                _db.Todos.Add(created);
            }
            else
            {
                existing.Title = change.Title;
                existing.Notes = change.Notes;
                existing.SetStatusFrom(change.Status, change.IsCompleted);
                existing.SortOrder = change.SortOrder;
                existing.Priority = Enum.Parse<PriorityLevel>(change.Priority);
                existing.DueDate = change.DueDate;
                existing.CategoryId = change.CategoryId;
                existing.UpdatedAt = change.UpdatedAt;
                existing.IsDeleted = change.IsDeleted;
            }
        }

        foreach (var change in request.Notes)
        {
            var existing = await _db.Notes
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(n => n.Id == change.Id && n.UserId == CurrentUserId);

            // Last-write-wins: only apply if the incoming change is newer
            if (existing != null && change.UpdatedAt <= existing.UpdatedAt)
                continue;

            if (existing is null)
            {
                _db.Notes.Add(new Note
                {
                    Title = change.Title,
                    Body = change.Body,
                    UpdatedAt = change.UpdatedAt,
                    IsDeleted = change.IsDeleted,
                    UserId = CurrentUserId,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.Title = change.Title;
                existing.Body = change.Body;
                existing.UpdatedAt = change.UpdatedAt;
                existing.IsDeleted = change.IsDeleted;
            }
        }

        foreach (var change in request.Meetings)
        {
            var existing = await _db.Meetings
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.Id == change.Id && m.UserId == CurrentUserId);

            // Last-write-wins: only apply if the incoming change is newer
            if (existing != null && change.UpdatedAt <= existing.UpdatedAt)
                continue;

            if (existing is null)
            {
                _db.Meetings.Add(new Meeting
                {
                    Title = change.Title,
                    Description = change.Description,
                    Location = change.Location,
                    StartsAt = change.StartsAt,
                    EndsAt = change.EndsAt,
                    MeetingNotes = change.MeetingNotes,
                    UpdatedAt = change.UpdatedAt,
                    IsDeleted = change.IsDeleted,
                    UserId = CurrentUserId,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.Title = change.Title;
                existing.Description = change.Description;
                existing.Location = change.Location;
                existing.StartsAt = change.StartsAt;
                existing.EndsAt = change.EndsAt;
                existing.MeetingNotes = change.MeetingNotes;
                existing.UpdatedAt = change.UpdatedAt;
                existing.IsDeleted = change.IsDeleted;
            }
        }

        await _db.SaveChangesAsync();
        return Ok(new { serverTime = DateTime.UtcNow });
    }
}
