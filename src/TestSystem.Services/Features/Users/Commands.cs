using MediatR;
using Microsoft.EntityFrameworkCore;
using TestSystem.Services.Common.Exceptions;
using TestSystem.Services.DTOs.Auth;
using TestSystem.Models.Entities;
using TestSystem.Models.Enums;

namespace TestSystem.Services.Features.Users;

// Get All Users
public record GetUsersQuery(UserRole? Role = null) : IRequest<List<UserDto>>;

public class GetUsersHandler : IRequestHandler<GetUsersQuery, List<UserDto>>
{
    private readonly DbContext _db;
    public GetUsersHandler(DbContext db) => _db = db;

    public async Task<List<UserDto>> Handle(GetUsersQuery request, CancellationToken ct)
    {
        var query = _db.Set<User>().AsQueryable();
        if (request.Role.HasValue) query = query.Where(u => u.Role == request.Role.Value);

        return await query.OrderBy(u => u.LastName).Select(u => new UserDto(
            u.Id, u.Email, u.FirstName, u.LastName,
            u.Role.ToString(), u.IsActive
        )).ToListAsync(ct);
    }
}

// Get User By Id
public record GetUserByIdQuery(Guid Id) : IRequest<UserDto>;

public class GetUserByIdHandler : IRequestHandler<GetUserByIdQuery, UserDto>
{
    private readonly DbContext _db;
    public GetUserByIdHandler(DbContext db) => _db = db;

    public async Task<UserDto> Handle(GetUserByIdQuery request, CancellationToken ct)
    {
        var u = await _db.Set<User>()
            .FirstOrDefaultAsync(u => u.Id == request.Id, ct)
            ?? throw new NotFoundException("User", request.Id);

        return new UserDto(u.Id, u.Email, u.FirstName, u.LastName,
            u.Role.ToString(), u.IsActive);
    }
}

// Update User (Admin: confirm teacher role + block account)
public record UpdateUserCommand(Guid Id, string? FirstName, string? LastName, UserRole? Role, bool? IsActive) : IRequest<UserDto>;

public class UpdateUserHandler : IRequestHandler<UpdateUserCommand, UserDto>
{
    private readonly DbContext _db;
    public UpdateUserHandler(DbContext db) => _db = db;

    public async Task<UserDto> Handle(UpdateUserCommand request, CancellationToken ct)
    {
        var user = await _db.Set<User>()
            .FirstOrDefaultAsync(u => u.Id == request.Id, ct)
            ?? throw new NotFoundException("User", request.Id);

        if (request.FirstName != null) user.FirstName = request.FirstName;
        if (request.LastName != null) user.LastName = request.LastName;
        if (request.Role.HasValue) user.Role = request.Role.Value;
        if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;

        await _db.SaveChangesAsync(ct);

        return new UserDto(user.Id, user.Email, user.FirstName, user.LastName,
            user.Role.ToString(), user.IsActive);
    }
}

// Delete User
public record DeleteUserCommand(Guid Id) : IRequest;

public class DeleteUserHandler : IRequestHandler<DeleteUserCommand>
{
    private readonly DbContext _db;
    public DeleteUserHandler(DbContext db) => _db = db;

    public async Task Handle(DeleteUserCommand request, CancellationToken ct)
    {
        var user = await _db.Set<User>().FindAsync(new object[] { request.Id }, ct)
            ?? throw new NotFoundException("User", request.Id);
        _db.Set<User>().Remove(user);
        await _db.SaveChangesAsync(ct);
    }
}
