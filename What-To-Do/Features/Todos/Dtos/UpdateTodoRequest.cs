namespace What_To_Do.Features.Todos.Dtos;
public class UpdateTodoRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsCompleted { get; set; }
    public string? Status { get; set; }               // Todo | InProgress | Done (wins over IsCompleted)
    public int? SortOrder { get; set; }
    public string Priority { get; set; } = "Medium";
    public DateTime? DueDate { get; set; }
    public int? CategoryId { get; set; }
}
