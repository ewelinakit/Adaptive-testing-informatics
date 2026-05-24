using TestSystem.Models.Enums;

namespace TestSystem.Services.DTOs.Users;

public record UpdateUserRequest(string? FirstName, string? LastName, UserRole? Role, bool? IsActive);
