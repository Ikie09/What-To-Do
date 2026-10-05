namespace What_To_Do.Features.Todos.Dtos;
public class ReorderTodoItem
{
    public int Id { get; set; }
    public string Status { get; set; } = "Todo";
    public int SortOrder { get; set; }
}
