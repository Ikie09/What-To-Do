using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using What_To_Do.Data;
using What_To_Do.Domain.Entities;
using What_To_Do.Features.Categories.Dtos;

namespace What_To_Do.Features.Categories;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly AppDbContext _db;
    public CategoriesController(AppDbContext db) => _db = db;

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // ── GET /api/categories ── (system defaults + this user's own) ──
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryResponse>>> GetAll()
    {
        var categories = await _db.Categories
            .Where(c => c.UserId == null || c.UserId == CurrentUserId)
            .Select(c => new CategoryResponse
            {
                Id = c.Id,
                Name = c.Name,
                Color = c.Color,
                TodoCount = c.Todos.Count(t => t.UserId == CurrentUserId)
            })
            .OrderBy(c => c.Name)
            .ToListAsync();

        return Ok(categories);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoryResponse>> GetById(int id)
    {
        var category = await _db.Categories
            .Where(c => c.Id == id && (c.UserId == null || c.UserId == CurrentUserId))
            .Select(c => new CategoryResponse
            {
                Id = c.Id,
                Name = c.Name,
                Color = c.Color,
                TodoCount = c.Todos.Count(t => t.UserId == CurrentUserId)
            })
            .FirstOrDefaultAsync();

        if (category is null) return NotFound(new { message = $"Category with id {id} not found." });

        return Ok(category);
    }

    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create([FromBody] CreateCategoryRequest dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { message = "Category name is required." });

        var exists = await _db.Categories.AnyAsync(c =>
            c.Name == dto.Name.Trim() && (c.UserId == null || c.UserId == CurrentUserId));
        if (exists)
            return Conflict(new { message = $"Category '{dto.Name}' already exists." });

        var category = new Category
        {
            Name = dto.Name.Trim(),
            Color = dto.Color,
            CreatedAt = DateTime.UtcNow,
            UserId = CurrentUserId
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = category.Id }, new CategoryResponse
        {
            Id = category.Id,
            Name = category.Name,
            Color = category.Color,
            TodoCount = 0
        });
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CategoryResponse>> Update(int id, [FromBody] UpdateCategoryRequest dto)
    {
        var category = await _db.Categories
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == CurrentUserId); // can't edit system defaults
        if (category is null) return NotFound(new { message = $"Category with id {id} not found." });

        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { message = "Category name is required." });

        var nameExists = await _db.Categories.AnyAsync(c =>
            c.Name == dto.Name.Trim() && c.Id != id && (c.UserId == null || c.UserId == CurrentUserId));
        if (nameExists)
            return Conflict(new { message = $"Category '{dto.Name}' already exists." });

        category.Name = dto.Name.Trim();
        category.Color = dto.Color;
        await _db.SaveChangesAsync();

        var todoCount = await _db.Todos.CountAsync(t => t.CategoryId == id && t.UserId == CurrentUserId);

        return Ok(new CategoryResponse
        {
            Id = category.Id,
            Name = category.Name,
            Color = category.Color,
            TodoCount = todoCount
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.UserId == CurrentUserId);
        if (category is null) return NotFound(new { message = $"Category with id {id} not found." });

        category.IsDeleted = true;
        category.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }
}