namespace What_To_Do.Features.Budgets.Dtos;

public class CreateBudgetRequest
{
    public string Category { get; set; } = string.Empty;
    public decimal MonthlyLimit { get; set; }
}

public class UpdateBudgetRequest
{
    public string Category { get; set; } = string.Empty;
    public decimal MonthlyLimit { get; set; }
}

public class BudgetResponse
{
    public int Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal MonthlyLimit { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
