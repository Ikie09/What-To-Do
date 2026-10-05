using Microsoft.AspNetCore.Identity;

namespace What_To_Do.Domain.Entities;
public class AppUser : IdentityUser
{
    public string? DisplayName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}