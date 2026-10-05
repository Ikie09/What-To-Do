namespace What_To_Do.Domain.Entities;
public class Note
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string UserId { get; set; } = string.Empty;
    public AppUser? User { get; set; }
    public bool IsDeleted { get; set; } = false;
}
