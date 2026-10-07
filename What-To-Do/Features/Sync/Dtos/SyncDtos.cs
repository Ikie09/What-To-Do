namespace What_To_Do.Features.Sync.Dtos;
public class SyncResponse
{
    public DateTime ServerTime { get; set; }
    public IEnumerable<TodoSyncDto> Todos { get; set; } = Enumerable.Empty<TodoSyncDto>();
    public IEnumerable<CategorySyncDto> Categories { get; set; } = Enumerable.Empty<CategorySyncDto>();
    public IEnumerable<NoteSyncDto> Notes { get; set; } = Enumerable.Empty<NoteSyncDto>();
    public IEnumerable<MeetingSyncDto> Meetings { get; set; } = Enumerable.Empty<MeetingSyncDto>();
    public IEnumerable<TransactionSyncDto> Transactions { get; set; } = Enumerable.Empty<TransactionSyncDto>();
    public IEnumerable<BudgetSyncDto> Budgets { get; set; } = Enumerable.Empty<BudgetSyncDto>();
}
public class TodoSyncDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsCompleted { get; set; }
    public string? Status { get; set; }
    public int SortOrder { get; set; }
    public string Priority { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public int? CategoryId { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

public class CategorySyncDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

public class MeetingSyncDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string? MeetingNotes { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

public class TransactionSyncDto
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime Date { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

public class BudgetSyncDto
{
    public int Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal MonthlyLimit { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

public class NoteSyncDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}