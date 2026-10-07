using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using What_To_Do.Data;
using What_To_Do.Domain.Entities;
using What_To_Do.Domain.Enums;
using What_To_Do.Features.Todos.Dtos;

namespace What_To_Do.Features.Todos;

[ApiController]
[Route("api/todos")]
[Authorize]
public class TodosController : ControllerBase
{
    private readonly AppDbContext _db;
    public TodosController(AppDbContext db) => _db = db;

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TodoResponse>>> GetAll(
        [FromQuery] bool? completed,
        [FromQuery] string? priority,
        [FromQuery] int? categoryId,
        [FromQuery] string? search)
    {
        var query = _db.Todos
            .Include(t => t.Category)
            .Where(t => t.UserId == CurrentUserId)
            .AsQueryable();

        if (completed.HasValue)
            query = query.Where(t => t.IsCompleted == completed.Value);

        if (!string.IsNullOrEmpty(priority) && Enum.TryParse<PriorityLevel>(priority, out var p))
            query = query.Where(t => t.Priority == p);

        if (categoryId.HasValue)
            query = query.Where(t => t.CategoryId == categoryId);

        if (!string.IsNullOrEmpty(search))
            query = query.Where(t => t.Title.Contains(search) ||
                                     t.Notes != null && t.Notes.Contains(search));

        var todos = await query
            .OrderBy(t => t.IsCompleted)
            .ThenBy(t => t.DueDate)
            .ThenByDescending(t => t.Priority)
            .Select(t => MapToDto(t))
            .ToListAsync();

        return Ok(todos);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TodoResponse>> GetById(int id)
    {
        var todo = await _db.Todos
            .Include(t => t.Category)
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == CurrentUserId); // FIXED

        if (todo is null) return NotFound(new { message = $"Todo with id {id} not found." });

        return Ok(MapToDto(todo));
    }

    [HttpPost]
    public async Task<ActionResult<TodoResponse>> Create([FromBody] CreateTodoRequest dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(new { message = "Title is required." });

        if (!Enum.TryParse<PriorityLevel>(dto.Priority, out var priority))
            return BadRequest(new { message = "Priority must be Low, Medium, or High." });

        if (dto.CategoryId.HasValue)
        {
            var categoryExists = await _db.Categories.AnyAsync(c =>
                c.Id == dto.CategoryId && (c.UserId == null || c.UserId == CurrentUserId));
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
            UpdatedAt = DateTime.UtcNow,
            UserId = CurrentUserId
        };
        todo.SetStatusFrom(dto.Status, false);

        // New cards go to the bottom of their column unless the client says otherwise.
        todo.SortOrder = dto.SortOrder ??
            (await _db.Todos.Where(t => t.UserId == CurrentUserId && t.Status == todo.Status)
                .Select(t => (int?)t.SortOrder).MaxAsync() ?? -1) + 1;

        _db.Todos.Add(todo);
        await _db.SaveChangesAsync();
        await _db.Entry(todo).Reference(t => t.Category).LoadAsync();

        return CreatedAtAction(nameof(GetById), new { id = todo.Id }, MapToDto(todo));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TodoResponse>> Update(int id, [FromBody] UpdateTodoRequest dto)
    {
        var todo = await _db.Todos.FirstOrDefaultAsync(t => t.Id == id && t.UserId == CurrentUserId); // FIXED
        if (todo is null) return NotFound(new { message = $"Todo with id {id} not found." });

        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(new { message = "Title is required." });

        if (!Enum.TryParse<PriorityLevel>(dto.Priority, out var priority))
            return BadRequest(new { message = "Priority must be Low, Medium, or High." });

        if (dto.CategoryId.HasValue)
        {
            var categoryExists = await _db.Categories.AnyAsync(c =>
                c.Id == dto.CategoryId && (c.UserId == null || c.UserId == CurrentUserId));
            if (!categoryExists)
                return BadRequest(new { message = $"Category with id {dto.CategoryId} not found." });
        }

        todo.Title = dto.Title.Trim();
        todo.Notes = dto.Notes?.Trim();
        todo.SetStatusFrom(dto.Status, dto.IsCompleted);
        if (dto.SortOrder.HasValue) todo.SortOrder = dto.SortOrder.Value;
        todo.Priority = priority;
        todo.DueDate = dto.DueDate;
        todo.CategoryId = dto.CategoryId;
        todo.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await _db.Entry(todo).Reference(t => t.Category).LoadAsync();

        return Ok(MapToDto(todo));
    }

    [HttpPatch("{id:int}/toggle")]
    public async Task<ActionResult<TodoResponse>> Toggle(int id)
    {
        var todo = await _db.Todos.Include(t => t.Category)
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == CurrentUserId); // FIXED
        if (todo is null) return NotFound(new { message = $"Todo with id {id} not found." });

        todo.SetStatus(todo.IsCompleted ? TodoStatus.Todo : TodoStatus.Done);
        todo.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(MapToDto(todo));
    }

    // One request per drag-and-drop: the client sends the new status/order for every card it touched.
    [HttpPost("reorder")]
    public async Task<ActionResult<IEnumerable<TodoResponse>>> Reorder([FromBody] List<ReorderTodoItem> items)
    {
        if (items.Count == 0) return Ok(Array.Empty<TodoResponse>());

        foreach (var item in items)
        {
            if (!Enum.TryParse<TodoStatus>(item.Status, out _))
                return BadRequest(new { message = "Status must be Todo, InProgress, or Done." });
        }

        var ids = items.Select(i => i.Id).ToList();
        var todos = await _db.Todos.Include(t => t.Category)
            .Where(t => t.UserId == CurrentUserId && ids.Contains(t.Id))
            .ToListAsync();

        if (todos.Count != ids.Distinct().Count())
            return NotFound(new { message = "One or more todos were not found." });

        var now = DateTime.UtcNow;
        foreach (var item in items)
        {
            var todo = todos.First(t => t.Id == item.Id);
            todo.SetStatus(Enum.Parse<TodoStatus>(item.Status));
            todo.SortOrder = item.SortOrder;
            todo.UpdatedAt = now;
        }

        await _db.SaveChangesAsync();
        return Ok(todos.Select(MapToDto));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var todo = await _db.Todos.FirstOrDefaultAsync(t => t.Id == id && t.UserId == CurrentUserId);
        if (todo is null) return NotFound(new { message = $"Todo with id {id} not found." });

        todo.IsDeleted = true;              
        todo.UpdatedAt = DateTime.UtcNow; 
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("completed")]
    public async Task<IActionResult> DeleteCompleted()
    {
        var completed = await _db.Todos
            .Where(t => t.IsCompleted && t.UserId == CurrentUserId)
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var t in completed)
        {
            t.IsDeleted = true;
            t.UpdatedAt = now;
        }
        await _db.SaveChangesAsync();

        return Ok(new { message = $"{completed.Count} completed todo(s) deleted." });
    }

    private static TodoResponse MapToDto(Todo t) => new()
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
        CategoryName = t.Category?.Name,
        CategoryColor = t.Category?.Color,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
        CompletedAt = t.CompletedAt
    };

}