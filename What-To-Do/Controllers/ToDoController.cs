using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;
using TodoApi.DTOs;
using TodoApi.Models;

namespace TodoApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ToDoController : ControllerBase
{
    private readonly AppDbContext _db;

    public ToDoController(AppDbContext db) => _db = db;

    // ── GET /api/todos ────────────────────────────────────────────
    // Supports: ?completed=true/false  ?priority=High  ?categoryId=1  ?search=text
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TodoResponseDto>>> GetAll(
        [FromQuery] bool? completed,
        [FromQuery] string? priority,
        [FromQuery] int? categoryId,
        [FromQuery] string? search)
    {
        var query = _db.Todos
                       .Include(t => t.Category)
                       .AsQueryable();

        if (completed.HasValue)
            query = query.Where(t => t.IsCompleted == completed.Value);

        if (!string.IsNullOrEmpty(priority) && Enum.TryParse<Priority>(priority, out var p))
            query = query.Where(t => t.Priority == p);

        if (categoryId.HasValue)
            query = query.Where(t => t.CategoryId == categoryId);

        if (!string.IsNullOrEmpty(search))
            query = query.Where(t => t.Title.Contains(search) ||
                                     (t.Notes != null && t.Notes.Contains(search)));

        var todos = await query
            .OrderBy(t => t.IsCompleted)
            .ThenBy(t => t.DueDate)
            .ThenByDescending(t => t.Priority)
            .Select(t => MapToDto(t))
            .ToListAsync();

        return Ok(todos);
    }

    // ── GET /api/todos/{id} ───────────────────────────────────────
    [HttpGet("{id:int}")]
    public async Task<ActionResult<TodoResponseDto>> GetById(int id)
    {
        var todo = await _db.Todos
                            .Include(t => t.Category)
                            .FirstOrDefaultAsync(t => t.Id == id);

        if (todo is null) return NotFound(new { message = $"Todo with id {id} not found." });

        return Ok(MapToDto(todo));
    }

    // ── POST /api/todos ───────────────────────────────────────────
    [HttpPost]
    public async Task<ActionResult<TodoResponseDto>> Create([FromBody] CreateTodoDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(new { message = "Title is required." });

        if (!Enum.TryParse<Priority>(dto.Priority, out var priority))
            return BadRequest(new { message = "Priority must be Low, Medium, or High." });

        // Validate category if provided
        if (dto.CategoryId.HasValue)
        {
            var categoryExists = await _db.Categories.AnyAsync(c => c.Id == dto.CategoryId);
            if (!categoryExists)
                return BadRequest(new { message = $"Category with id {dto.CategoryId} not found." });
        }

        var todo = new Todo
        {
            Title = dto.Title.Trim(),
            Notes = dto.Notes?.Trim(),
            Priority = priority,
            DueDate = dto.DueDate,
            CategoryId = dto.CategoryId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Todos.Add(todo);
        await _db.SaveChangesAsync();

        // Reload with category
        await _db.Entry(todo).Reference(t => t.Category).LoadAsync();

        return CreatedAtAction(nameof(GetById), new { id = todo.Id }, MapToDto(todo));
    }

    // ── PUT /api/todos/{id} ───────────────────────────────────────
    [HttpPut("{id:int}")]
    public async Task<ActionResult<TodoResponseDto>> Update(int id, [FromBody] UpdateTodoDto dto)
    {
        var todo = await _db.Todos.FindAsync(id);
        if (todo is null) return NotFound(new { message = $"Todo with id {id} not found." });

        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(new { message = "Title is required." });

        if (!Enum.TryParse<Priority>(dto.Priority, out var priority))
            return BadRequest(new { message = "Priority must be Low, Medium, or High." });

        if (dto.CategoryId.HasValue)
        {
            var categoryExists = await _db.Categories.AnyAsync(c => c.Id == dto.CategoryId);
            if (!categoryExists)
                return BadRequest(new { message = $"Category with id {dto.CategoryId} not found." });
        }

        todo.Title = dto.Title.Trim();
        todo.Notes = dto.Notes?.Trim();
        todo.IsCompleted = dto.IsCompleted;
        todo.Priority = priority;
        todo.DueDate = dto.DueDate;
        todo.CategoryId = dto.CategoryId;
        todo.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await _db.Entry(todo).Reference(t => t.Category).LoadAsync();

        return Ok(MapToDto(todo));
    }

    // ── PATCH /api/todos/{id}/toggle ─────────────────────────────
    // Quick toggle complete/incomplete without sending full body
    [HttpPatch("{id:int}/toggle")]
    public async Task<ActionResult<TodoResponseDto>> Toggle(int id)
    {
        var todo = await _db.Todos.Include(t => t.Category).FirstOrDefaultAsync(t => t.Id == id);
        if (todo is null) return NotFound(new { message = $"Todo with id {id} not found." });

        todo.IsCompleted = !todo.IsCompleted;
        todo.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(MapToDto(todo));
    }

    // ── DELETE /api/todos/{id} ────────────────────────────────────
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var todo = await _db.Todos.FindAsync(id);
        if (todo is null) return NotFound(new { message = $"Todo with id {id} not found." });

        _db.Todos.Remove(todo);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // ── DELETE /api/todos/completed ───────────────────────────────
    // Bulk delete all completed todos
    [HttpDelete("completed")]
    public async Task<IActionResult> DeleteCompleted()
    {
        var completed = await _db.Todos.Where(t => t.IsCompleted).ToListAsync();
        _db.Todos.RemoveRange(completed);
        await _db.SaveChangesAsync();

        return Ok(new { message = $"{completed.Count} completed todo(s) deleted." });
    }

    // ── Mapper helper ─────────────────────────────────────────────
    private static TodoResponseDto MapToDto(Todo t) => new()
    {
        Id = t.Id,
        Title = t.Title,
        Notes = t.Notes,
        IsCompleted = t.IsCompleted,
        Priority = t.Priority.ToString(),
        DueDate = t.DueDate,
        CategoryId = t.CategoryId,
        CategoryName = t.Category?.Name,
        CategoryColor = t.Category?.Color,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt
    };
}