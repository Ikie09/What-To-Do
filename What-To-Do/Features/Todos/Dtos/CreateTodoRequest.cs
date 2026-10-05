namespace What_To_Do.Features.Todos.Dtos;
public class CreateTodoRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string Priority { get; set; } = "Medium";  // Low | Medium | High
    public string? Status { get; set; }               // Todo | InProgress | Done
    public int? SortOrder { get; set; }
    public DateTime? DueDate { get; set; }
    public int? CategoryId { get; set; }
}
