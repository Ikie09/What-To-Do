using What_To_Do.Domain.Enums;

namespace What_To_Do.Domain.Entities;
public class Transaction
{
    public int Id { get; set; }
    public TransactionType Type { get; set; } = TransactionType.Expense;
    public decimal Amount { get; set; }
    // Free text on purpose: budgets match on the name, so offline-created records never reference row ids.
    public string Category { get; set; } = string.Empty;
    public string? Note { get; set; }
    // Date-only value, stored as UTC midnight.
    public DateTime Date { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string UserId { get; set; } = string.Empty;
    public AppUser? User { get; set; }
    public bool IsDeleted { get; set; } = false;
}
