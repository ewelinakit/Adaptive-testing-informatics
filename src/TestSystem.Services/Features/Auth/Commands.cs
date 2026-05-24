using MediatR;
using Microsoft.EntityFrameworkCore;
using TestSystem.Services.Common.Exceptions;
using TestSystem.Services.DTOs.Auth;
using TestSystem.Services.Interfaces;
using TestSystem.Models.Entities;
using TestSystem.Models.Enums;

namespace TestSystem.Services.Features.Auth;

// Login
public record LoginCommand(string Email, string Password) : IRequest<AuthResponse>;

public class LoginHandler : IRequestHandler<LoginCommand, AuthResponse>
{
    private readonly DbContext _db;
    private readonly ITokenService _tokenService;

    public LoginHandler(DbContext db, ITokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    public async Task<AuthResponse> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await _db.Set<User>()
            .FirstOrDefaultAsync(u => u.Email == request.Email, ct)
            ?? throw new BadRequestException("Невірний email або пароль");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new BadRequestException("Невірний email або пароль");

        if (!user.IsActive)
            throw new ForbiddenException("Акаунт деактивовано");

        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshTokenStr = _tokenService.GenerateRefreshToken();

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshTokenStr,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _db.Set<RefreshToken>().Add(refreshToken);
        await _db.SaveChangesAsync(ct);

        return new AuthResponse(MapUser(user), accessToken, refreshTokenStr);
    }

    private static UserDto MapUser(User u) => new(
        u.Id, u.Email, u.FirstName, u.LastName, u.Role.ToString(), u.IsActive);
}

// Register
public record RegisterCommand(string Email, string Password, string FirstName, string LastName, UserRole Role) : IRequest<AuthResponse>;

public class RegisterHandler : IRequestHandler<RegisterCommand, AuthResponse>
{
    private readonly DbContext _db;
    private readonly ITokenService _tokenService;

    public RegisterHandler(DbContext db, ITokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    public async Task<AuthResponse> Handle(RegisterCommand request, CancellationToken ct)
    {
        if (await _db.Set<User>().AnyAsync(u => u.Email == request.Email, ct))
            throw new BadRequestException("Користувач з таким email вже існує");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Role = request.Role
        };

        _db.Set<User>().Add(user);

        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshTokenStr = _tokenService.GenerateRefreshToken();

        _db.Set<RefreshToken>().Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshTokenStr,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });

        await _db.SaveChangesAsync(ct);

        return new AuthResponse(
            new UserDto(user.Id, user.Email, user.FirstName, user.LastName, user.Role.ToString(), user.IsActive),
            accessToken, refreshTokenStr);
    }
}

// Refresh Token
public record RefreshTokenCommand(string AccessToken, string RefreshToken) : IRequest<AuthResponse>;

public class RefreshTokenHandler : IRequestHandler<RefreshTokenCommand, AuthResponse>
{
    private readonly DbContext _db;
    private readonly ITokenService _tokenService;

    public RefreshTokenHandler(DbContext db, ITokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    public async Task<AuthResponse> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var principal = _tokenService.GetPrincipalFromExpiredToken(request.AccessToken)
            ?? throw new BadRequestException("Невірний токен");

        var userId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? throw new BadRequestException("Невірний токен");

        var storedToken = await _db.Set<RefreshToken>()
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken && rt.UserId == Guid.Parse(userId), ct)
            ?? throw new BadRequestException("Невірний refresh токен");

        if (!storedToken.IsActive)
            throw new BadRequestException("Refresh токен вже не дійсний");

        storedToken.RevokedAt = DateTime.UtcNow;

        var user = await _db.Set<User>()
            .FirstAsync(u => u.Id == Guid.Parse(userId), ct);

        var newAccessToken = _tokenService.GenerateAccessToken(user);
        var newRefreshTokenStr = _tokenService.GenerateRefreshToken();

        _db.Set<RefreshToken>().Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = newRefreshTokenStr,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });

        await _db.SaveChangesAsync(ct);

        return new AuthResponse(
            new UserDto(user.Id, user.Email, user.FirstName, user.LastName, user.Role.ToString(), user.IsActive),
            newAccessToken, newRefreshTokenStr);
    }
}

// Revoke Token
public record RevokeTokenCommand(string RefreshToken) : IRequest;

public class RevokeTokenHandler : IRequestHandler<RevokeTokenCommand>
{
    private readonly DbContext _db;

    public RevokeTokenHandler(DbContext db)
    {
        _db = db;
    }

    public async Task Handle(RevokeTokenCommand request, CancellationToken ct)
    {
        var token = await _db.Set<RefreshToken>()
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken, ct);

        if (token != null)
        {
            token.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }
}

// Get Current User
public record GetCurrentUserQuery(Guid UserId) : IRequest<UserDto>;

public class GetCurrentUserHandler : IRequestHandler<GetCurrentUserQuery, UserDto>
{
    private readonly DbContext _db;

    public GetCurrentUserHandler(DbContext db)
    {
        _db = db;
    }

    public async Task<UserDto> Handle(GetCurrentUserQuery request, CancellationToken ct)
    {
        var user = await _db.Set<User>()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, ct)
            ?? throw new NotFoundException("User", request.UserId);

        return new UserDto(user.Id, user.Email, user.FirstName, user.LastName,
            user.Role.ToString(), user.IsActive);
    }
}
