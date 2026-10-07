using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using What_To_Do.Data;
using What_To_Do.Domain.Entities;
using What_To_Do.Domain.Enums;
using What_To_Do.Features.Transactions.Dtos;

namespace What_To_Do.Features.Transactions;

[ApiController]
[Route("api/transactions")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private const decimal MaxAmount = 1_000_000_000_000m;

    private readonly AppDbContext _db;
    public TransactionsController(AppDbContext db) => _db = db;

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TransactionResponse>>> GetAll(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? type,
        [FromQuery] string? category,
        [FromQuery] string? search)
    {
        var query = _db.Transactions
            .Where(t => t.UserId == CurrentUserId)
            .AsQueryable();

        if (from.HasValue) query = query.Where(t => t.Date >= from.Value.Date);
        if (to.HasValue) query = query.Where(t => t.Date <= to.Value.Date);

        if (!string.IsNullOrEmpty(type) && Enum.TryParse<TransactionType>(type, out var parsedType))
            query = query.Where(t => t.Type == parsedType);

        if (!string.IsNullOrEmpty(category))
            query = query.Where(t => t.Category == category);

        if (!string.IsNullOrEmpty(search))
            query = query.Where(t => t.Category.Contains(search) ||
                                     t.Note != null && t.Note.Contains(search));

        var items = await query
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .Select(t => MapToDto(t))
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TransactionResponse>> GetById(int id)
    {
        var item = await _db.Transactions.FirstOrDefaultAsync(t => t.Id == id && t.UserId == CurrentUserId);
        if (item is null) return NotFound(new { message = $"Transaction with id {id} not found." });

        return Ok(MapToDto(item));
    }

    [HttpPost]
    public async Task<ActionResult<TransactionResponse>> Create([FromBody] CreateTransactionRequest dto)
    {
        var error = Validate(dto.Type, dto.Amount, dto.Category, out var type);
        if (error is not null) return BadRequest(new { message = error });

        var item = new Transaction
        {
            Type = type,
            Amount = dto.Amount,
            Category = dto.Category.Trim(),
            Note = dto.Note?.Trim(),
            Date = dto.Date.Date,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            UserId = CurrentUserId
        };

        _db.Transactions.Add(item);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = item.Id }, MapToDto(item));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TransactionResponse>> Update(int id, [FromBody] UpdateTransactionRequest dto)
    {
        var item = await _db.Transactions.FirstOrDefaultAsync(t => t.Id == id && t.UserId == CurrentUserId);
        if (item is null) return NotFound(new { message = $"Transaction with id {id} not found." });

        var error = Validate(dto.Type, dto.Amount, dto.Category, out var type);
        if (error is not null) return BadRequest(new { message = error });

        item.Type = type;
        item.Amount = dto.Amount;
        item.Category = dto.Category.Trim();
        item.Note = dto.Note?.Trim();
        item.Date = dto.Date.Date;
        item.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(MapToDto(item));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.Transactions.FirstOrDefaultAsync(t => t.Id == id && t.UserId == CurrentUserId);
        if (item is null) return NotFound(new { message = $"Transaction with id {id} not found." });

        item.IsDeleted = true;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private static string? Validate(string typeName, decimal amount, string category, out TransactionType type)
    {
        if (!Enum.TryParse(typeName, out type))
            return "Type must be Income or Expense.";
        if (amount <= 0 || amount > MaxAmount)
            return "Amount must be greater than 0.";
        if (string.IsNullOrWhiteSpace(category))
            return "Category is required.";
        if (category.Trim().Length > 100)
            return "Category must be 100 characters or fewer.";
        return null;
    }

    private static TransactionResponse MapToDto(Transaction t) => new()
    {
        Id = t.Id,
        Type = t.Type.ToString(),
        Amount = t.Amount,
        Category = t.Category,
        Note = t.Note,
        Date = t.Date,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt
    };
}
