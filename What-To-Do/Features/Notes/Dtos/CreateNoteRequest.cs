namespace What_To_Do.Features.Notes.Dtos;
public class CreateNoteRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
}
