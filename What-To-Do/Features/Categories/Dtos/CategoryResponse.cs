namespace What_To_Do.Features.Categories.Dtos;

public class CategoryResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public int TodoCount { get; set; }
}
