namespace What_To_Do.Features.Auth.Dtos;

public record RegisterRequest(string Email, string Password, string? DisplayName);
public record LoginRequest(string Email, string Password);
