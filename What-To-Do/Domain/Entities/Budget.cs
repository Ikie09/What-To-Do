namespace What_To_Do.Domain.Entities;
public class Budget
{
    public int Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal MonthlyLimit { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string UserId { get; set; } = string.Empty;
    public AppUser? User { get; set; }
    public bool IsDeleted { get; set; } = false;
}
