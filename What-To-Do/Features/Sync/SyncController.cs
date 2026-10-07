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

    private const decimal MaxAmount = 1_000_000_000_000m;

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
                CompletedAt = t.CompletedAt,
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

        var transactions = await _db.Transactions
            .IgnoreQueryFilters()
            .Where(t => t.UserId == CurrentUserId && t.UpdatedAt > cutoff)
            .Select(t => new TransactionSyncDto
            {
                Id = t.Id,
                Type = t.Type.ToString(),
                Amount = t.Amount,
                Category = t.Category,
                Note = t.Note,
                Date = t.Date,
                UpdatedAt = t.UpdatedAt,
                IsDeleted = t.IsDeleted
            })
            .ToListAsync();

        var budgets = await _db.Budgets
            .IgnoreQueryFilters()
            .Where(b => b.UserId == CurrentUserId && b.UpdatedAt > cutoff)
            .Select(b => new BudgetSyncDto
            {
                Id = b.Id,
                Category = b.Category,
                MonthlyLimit = b.MonthlyLimit,
                UpdatedAt = b.UpdatedAt,
                IsDeleted = b.IsDeleted
            })
            .ToListAsync();

        return Ok(new SyncResponse
        {
            ServerTime = serverTime,
            Todos = todos,
            Categories = categories,
            Notes = notes,
            Meetings = meetings,
            Transactions = transactions,
            Budgets = budgets
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
                created.SetStatusFrom(change.Status, change.IsCompleted, change.CompletedAt);
                _db.Todos.Add(created);
            }
            else
            {
                existing.Title = change.Title;
                existing.Notes = change.Notes;
                existing.SetStatusFrom(change.Status, change.IsCompleted, change.CompletedAt);
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

        foreach (var change in request.Transactions)
        {
            if (!Enum.TryParse<TransactionType>(change.Type, out var type) || change.Amount <= 0 ||
                change.Amount > MaxAmount || string.IsNullOrWhiteSpace(change.Category))
                continue; // an invalid queued record must not block the rest of the batch

            var existing = await _db.Transactions
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == change.Id && t.UserId == CurrentUserId);

            // Last-write-wins: only apply if the incoming change is newer
            if (existing != null && change.UpdatedAt <= existing.UpdatedAt)
                continue;

            if (existing is null)
            {
                _db.Transactions.Add(new Transaction
                {
                    Type = type,
                    Amount = change.Amount,
                    Category = change.Category.Trim(),
                    Note = change.Note,
                    Date = change.Date,
                    UpdatedAt = change.UpdatedAt,
                    IsDeleted = change.IsDeleted,
                    UserId = CurrentUserId,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.Type = type;
                existing.Amount = change.Amount;
                existing.Category = change.Category.Trim();
                existing.Note = change.Note;
                existing.Date = change.Date;
                existing.UpdatedAt = change.UpdatedAt;
                existing.IsDeleted = change.IsDeleted;
            }
        }

        foreach (var change in request.Budgets)
        {
            if (change.MonthlyLimit < 0 || change.MonthlyLimit > MaxAmount || string.IsNullOrWhiteSpace(change.Category))
                continue;

            var existing = await _db.Budgets
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(b => b.Id == change.Id && b.UserId == CurrentUserId);

            // Last-write-wins: only apply if the incoming change is newer
            if (existing != null && change.UpdatedAt <= existing.UpdatedAt)
                continue;

            if (existing is null)
            {
                _db.Budgets.Add(new Budget
                {
                    Category = change.Category.Trim(),
                    MonthlyLimit = change.MonthlyLimit,
                    UpdatedAt = change.UpdatedAt,
                    IsDeleted = change.IsDeleted,
                    UserId = CurrentUserId,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.Category = change.Category.Trim();
                existing.MonthlyLimit = change.MonthlyLimit;
                existing.UpdatedAt = change.UpdatedAt;
                existing.IsDeleted = change.IsDeleted;
            }
        }

        await _db.SaveChangesAsync();
        return Ok(new { serverTime = DateTime.UtcNow });
    }
}
