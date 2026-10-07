using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using What_To_Do.Data;
using What_To_Do.Domain.Entities;
using What_To_Do.Features.Budgets.Dtos;

namespace What_To_Do.Features.Budgets;

[ApiController]
[Route("api/budgets")]
[Authorize]
public class BudgetsController : ControllerBase
{
    private const decimal MaxAmount = 1_000_000_000_000m;

    private readonly AppDbContext _db;
    public BudgetsController(AppDbContext db) => _db = db;

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BudgetResponse>>> GetAll()
    {
        var items = await _db.Budgets
            .Where(b => b.UserId == CurrentUserId)
            .OrderBy(b => b.Category)
            .Select(b => MapToDto(b))
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BudgetResponse>> GetById(int id)
    {
        var item = await _db.Budgets.FirstOrDefaultAsync(b => b.Id == id && b.UserId == CurrentUserId);
        if (item is null) return NotFound(new { message = $"Budget with id {id} not found." });

        return Ok(MapToDto(item));
    }

    [HttpPost]
    public async Task<ActionResult<BudgetResponse>> Create([FromBody] CreateBudgetRequest dto)
    {
        var error = Validate(dto.Category, dto.MonthlyLimit);
        if (error is not null) return BadRequest(new { message = error });

        var category = dto.Category.Trim();
        var exists = await _db.Budgets.AnyAsync(b => b.UserId == CurrentUserId && b.Category == category);
        if (exists) return Conflict(new { message = $"A budget for '{category}' already exists." });

        var item = new Budget
        {
            Category = category,
            MonthlyLimit = dto.MonthlyLimit,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            UserId = CurrentUserId
        };

        _db.Budgets.Add(item);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = item.Id }, MapToDto(item));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<BudgetResponse>> Update(int id, [FromBody] UpdateBudgetRequest dto)
    {
        var item = await _db.Budgets.FirstOrDefaultAsync(b => b.Id == id && b.UserId == CurrentUserId);
        if (item is null) return NotFound(new { message = $"Budget with id {id} not found." });

        var error = Validate(dto.Category, dto.MonthlyLimit);
        if (error is not null) return BadRequest(new { message = error });

        var category = dto.Category.Trim();
        var clash = await _db.Budgets.AnyAsync(b => b.UserId == CurrentUserId && b.Category == category && b.Id != id);
        if (clash) return Conflict(new { message = $"A budget for '{category}' already exists." });

        item.Category = category;
        item.MonthlyLimit = dto.MonthlyLimit;
        item.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(MapToDto(item));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.Budgets.FirstOrDefaultAsync(b => b.Id == id && b.UserId == CurrentUserId);
        if (item is null) return NotFound(new { message = $"Budget with id {id} not found." });

        item.IsDeleted = true;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private static string? Validate(string category, decimal limit)
    {
        if (string.IsNullOrWhiteSpace(category))
            return "Category is required.";
        if (category.Trim().Length > 100)
            return "Category must be 100 characters or fewer.";
        if (limit < 0 || limit > MaxAmount)
            return "Monthly limit must be 0 or more.";
        return null;
    }

    private static BudgetResponse MapToDto(Budget b) => new()
    {
        Id = b.Id,
        Category = b.Category,
        MonthlyLimit = b.MonthlyLimit,
        CreatedAt = b.CreatedAt,
        UpdatedAt = b.UpdatedAt
    };
}
