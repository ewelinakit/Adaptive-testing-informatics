using TestSystem.Models.Enums;

namespace TestSystem.Services.DTOs.Auth;

public record LoginRequest(string Email, string Password);
public record RegisterRequest(string Email, string Password, string FirstName, string LastName, UserRole Role);
public record RefreshTokenRequest(string AccessToken, string RefreshToken);
public record RevokeTokenRequest(string RefreshToken);

public record AuthResponse(UserDto User, string AccessToken, string RefreshToken);

public record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    bool IsActive);
