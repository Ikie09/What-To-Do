using What_To_Do.Domain.Enums;

namespace What_To_Do.Domain.Entities;
public class Todo
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsCompleted { get; set; } = false;
    public TodoStatus Status { get; set; } = TodoStatus.Todo;
    public int SortOrder { get; set; } = 0;
    public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;
    public DateTime? DueDate { get; set; }
    public int? CategoryId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string UserId { get; set; } = string.Empty;
    public AppUser? User { get; set; }
    public Category? Category { get; set; }
    public bool IsDeleted { get; set; } = false;

    // Status and IsCompleted must never disagree: Done <=> IsCompleted.
    public void SetStatus(TodoStatus status)
    {
        Status = status;
        IsCompleted = status == TodoStatus.Done;
    }

    // Accepts a status name from a client; falls back to the legacy IsCompleted flag.
    public void SetStatusFrom(string? statusName, bool isCompleted)
    {
        if (!string.IsNullOrEmpty(statusName) && Enum.TryParse<TodoStatus>(statusName, out var parsed))
            SetStatus(parsed);
        else
            SetStatus(isCompleted ? TodoStatus.Done : TodoStatus.Todo);
    }
}
