using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using What_To_Do.Data;
using What_To_Do.Domain.Entities;
using What_To_Do.Features.Notes.Dtos;

namespace What_To_Do.Features.Notes;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotesController : ControllerBase
{
    private readonly AppDbContext _db;
    public NotesController(AppDbContext db) => _db = db;

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NoteResponse>>> GetAll([FromQuery] string? search)
    {
        var query = _db.Notes
            .Where(n => n.UserId == CurrentUserId)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(n => n.Title.Contains(search) ||
                                     n.Body != null && n.Body.Contains(search));

        var notes = await query
            .OrderByDescending(n => n.UpdatedAt)
            .Select(n => MapToDto(n))
            .ToListAsync();

        return Ok(notes);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<NoteResponse>> GetById(int id)
    {
        var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == CurrentUserId);
        if (note is null) return NotFound(new { message = $"Note with id {id} not found." });

        return Ok(MapToDto(note));
    }

    [HttpPost]
    public async Task<ActionResult<NoteResponse>> Create([FromBody] CreateNoteRequest dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(new { message = "Title is required." });

        var note = new Note
        {
            Title = dto.Title.Trim(),
            Body = dto.Body?.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            UserId = CurrentUserId
        };

        _db.Notes.Add(note);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = note.Id }, MapToDto(note));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<NoteResponse>> Update(int id, [FromBody] UpdateNoteRequest dto)
    {
        var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == CurrentUserId);
        if (note is null) return NotFound(new { message = $"Note with id {id} not found." });

        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(new { message = "Title is required." });

        note.Title = dto.Title.Trim();
        note.Body = dto.Body?.Trim();
        note.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(MapToDto(note));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == CurrentUserId);
        if (note is null) return NotFound(new { message = $"Note with id {id} not found." });

        note.IsDeleted = true;
        note.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private static NoteResponse MapToDto(Note n) => new()
    {
        Id = n.Id,
        Title = n.Title,
        Body = n.Body,
        CreatedAt = n.CreatedAt,
        UpdatedAt = n.UpdatedAt
    };
}
