namespace What_To_Do.Domain.Entities;
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;   
    public bool IsDeleted { get; set; } = false;
    public string? UserId { get; set; }
    public AppUser? User { get; set; }
    public ICollection<Todo> Todos { get; set; } = new List<Todo>();
}
