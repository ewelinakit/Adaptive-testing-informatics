using MediatR;
using Microsoft.EntityFrameworkCore;
using TestSystem.Services.Common.Exceptions;
using TestSystem.Services.DTOs.Groups;
using TestSystem.Models.Entities;

namespace TestSystem.Services.Features.Groups;

// Create Group (Teacher)
public record CreateGroupCommand(string Name, string? Description, Guid TeacherId) : IRequest<GroupDto>;

public class CreateGroupHandler : IRequestHandler<CreateGroupCommand, GroupDto>
{
    private readonly DbContext _db;
    public CreateGroupHandler(DbContext db) => _db = db;

    public async Task<GroupDto> Handle(CreateGroupCommand request, CancellationToken ct)
    {
        var teacher = await _db.Set<User>().FindAsync(new object[] { request.TeacherId }, ct)
            ?? throw new NotFoundException("User", request.TeacherId);

        var inviteCode = GenerateInviteCode();
        while (await _db.Set<Group>().AnyAsync(g => g.InviteCode == inviteCode, ct))
            inviteCode = GenerateInviteCode();

        var group = new Group
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            InviteCode = inviteCode,
            TeacherId = request.TeacherId
        };

        _db.Set<Group>().Add(group);
        await _db.SaveChangesAsync(ct);

        return new GroupDto(group.Id, group.Name, group.Description, group.InviteCode,
            $"{teacher.FirstName} {teacher.LastName}", 0, group.IsActive, group.CreatedAt);
    }

    private static string GenerateInviteCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Range(0, 8).Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }
}

// Get My Groups (Teacher)
public record GetMyGroupsQuery(Guid TeacherId) : IRequest<List<GroupDto>>;

public class GetMyGroupsHandler : IRequestHandler<GetMyGroupsQuery, List<GroupDto>>
{
    private readonly DbContext _db;
    public GetMyGroupsHandler(DbContext db) => _db = db;

    public async Task<List<GroupDto>> Handle(GetMyGroupsQuery request, CancellationToken ct)
    {
        return await _db.Set<Group>()
            .Include(g => g.Teacher)
            .Include(g => g.Members)
            .Where(g => g.TeacherId == request.TeacherId)
            .OrderByDescending(g => g.CreatedAt)
            .Select(g => new GroupDto(g.Id, g.Name, g.Description, g.InviteCode,
                g.Teacher.FirstName + " " + g.Teacher.LastName,
                g.Members.Count, g.IsActive, g.CreatedAt))
            .ToListAsync(ct);
    }
}

// Get Group By Id
public record GetGroupByIdQuery(Guid Id) : IRequest<GroupDto>;

public class GetGroupByIdHandler : IRequestHandler<GetGroupByIdQuery, GroupDto>
{
    private readonly DbContext _db;
    public GetGroupByIdHandler(DbContext db) => _db = db;

    public async Task<GroupDto> Handle(GetGroupByIdQuery request, CancellationToken ct)
    {
        var g = await _db.Set<Group>()
            .Include(g => g.Teacher)
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == request.Id, ct)
            ?? throw new NotFoundException("Group", request.Id);

        return new GroupDto(g.Id, g.Name, g.Description, g.InviteCode,
            $"{g.Teacher.FirstName} {g.Teacher.LastName}",
            g.Members.Count, g.IsActive, g.CreatedAt);
    }
}

// Update Group
public record UpdateGroupCommand(Guid Id, Guid TeacherId, string? Name, string? Description, bool? IsActive) : IRequest<GroupDto>;

public class UpdateGroupHandler : IRequestHandler<UpdateGroupCommand, GroupDto>
{
    private readonly DbContext _db;
    public UpdateGroupHandler(DbContext db) => _db = db;

    public async Task<GroupDto> Handle(UpdateGroupCommand request, CancellationToken ct)
    {
        var group = await _db.Set<Group>()
            .Include(g => g.Teacher)
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == request.Id && g.TeacherId == request.TeacherId, ct)
            ?? throw new NotFoundException("Group", request.Id);

        if (request.Name != null) group.Name = request.Name;
        if (request.Description != null) group.Description = request.Description;
        if (request.IsActive.HasValue) group.IsActive = request.IsActive.Value;

        await _db.SaveChangesAsync(ct);

        return new GroupDto(group.Id, group.Name, group.Description, group.InviteCode,
            $"{group.Teacher.FirstName} {group.Teacher.LastName}",
            group.Members.Count, group.IsActive, group.CreatedAt);
    }
}

// Delete Group
public record DeleteGroupCommand(Guid Id, Guid TeacherId) : IRequest;

public class DeleteGroupHandler : IRequestHandler<DeleteGroupCommand>
{
    private readonly DbContext _db;
    public DeleteGroupHandler(DbContext db) => _db = db;

    public async Task Handle(DeleteGroupCommand request, CancellationToken ct)
    {
        var group = await _db.Set<Group>()
            .FirstOrDefaultAsync(g => g.Id == request.Id && g.TeacherId == request.TeacherId, ct)
            ?? throw new NotFoundException("Group", request.Id);

        _db.Set<Group>().Remove(group);
        await _db.SaveChangesAsync(ct);
    }
}

// Get Group Members
public record GetGroupMembersQuery(Guid GroupId) : IRequest<List<GroupMemberDto>>;

public class GetGroupMembersHandler : IRequestHandler<GetGroupMembersQuery, List<GroupMemberDto>>
{
    private readonly DbContext _db;
    public GetGroupMembersHandler(DbContext db) => _db = db;

    public async Task<List<GroupMemberDto>> Handle(GetGroupMembersQuery request, CancellationToken ct)
    {
        return await _db.Set<GroupMember>()
            .Include(gm => gm.Student)
            .Where(gm => gm.GroupId == request.GroupId)
            .OrderBy(gm => gm.Student.LastName)
            .Select(gm => new GroupMemberDto(gm.Id, gm.StudentId,
                gm.Student.FirstName + " " + gm.Student.LastName,
                gm.Student.Email, gm.JoinedAt))
            .ToListAsync(ct);
    }
}

// Remove Group Member
public record RemoveGroupMemberCommand(Guid GroupId, Guid MemberId, Guid TeacherId) : IRequest;

public class RemoveGroupMemberHandler : IRequestHandler<RemoveGroupMemberCommand>
{
    private readonly DbContext _db;
    public RemoveGroupMemberHandler(DbContext db) => _db = db;

    public async Task Handle(RemoveGroupMemberCommand request, CancellationToken ct)
    {
        var group = await _db.Set<Group>()
            .FirstOrDefaultAsync(g => g.Id == request.GroupId && g.TeacherId == request.TeacherId, ct)
            ?? throw new NotFoundException("Group", request.GroupId);

        var member = await _db.Set<GroupMember>()
            .FirstOrDefaultAsync(gm => gm.Id == request.MemberId && gm.GroupId == request.GroupId, ct)
            ?? throw new NotFoundException("GroupMember", request.MemberId);

        _db.Set<GroupMember>().Remove(member);
        await _db.SaveChangesAsync(ct);
    }
}

// Join Group By Code (Student)
public record JoinGroupByCodeCommand(string InviteCode, Guid StudentId) : IRequest<GroupDto>;

public class JoinGroupByCodeHandler : IRequestHandler<JoinGroupByCodeCommand, GroupDto>
{
    private readonly DbContext _db;
    public JoinGroupByCodeHandler(DbContext db) => _db = db;

    public async Task<GroupDto> Handle(JoinGroupByCodeCommand request, CancellationToken ct)
    {
        var group = await _db.Set<Group>()
            .Include(g => g.Teacher)
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.InviteCode == request.InviteCode && g.IsActive, ct)
            ?? throw new BadRequestException("Група з таким кодом не знайдена або неактивна");

        if (group.Members.Any(m => m.StudentId == request.StudentId))
            throw new BadRequestException("Ви вже є учасником цієї групи");

        var member = new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = group.Id,
            StudentId = request.StudentId,
            JoinedAt = DateTime.UtcNow
        };

        _db.Set<GroupMember>().Add(member);
        await _db.SaveChangesAsync(ct);

        return new GroupDto(group.Id, group.Name, group.Description, group.InviteCode,
            $"{group.Teacher.FirstName} {group.Teacher.LastName}",
            group.Members.Count + 1, group.IsActive, group.CreatedAt);
    }
}

// Get My Groups As Student
public record GetMyGroupsAsStudentQuery(Guid StudentId) : IRequest<List<GroupDto>>;

public class GetMyGroupsAsStudentHandler : IRequestHandler<GetMyGroupsAsStudentQuery, List<GroupDto>>
{
    private readonly DbContext _db;
    public GetMyGroupsAsStudentHandler(DbContext db) => _db = db;

    public async Task<List<GroupDto>> Handle(GetMyGroupsAsStudentQuery request, CancellationToken ct)
    {
        return await _db.Set<GroupMember>()
            .Include(gm => gm.Group).ThenInclude(g => g.Teacher)
            .Include(gm => gm.Group).ThenInclude(g => g.Members)
            .Where(gm => gm.StudentId == request.StudentId && gm.Group.IsActive)
            .OrderByDescending(gm => gm.JoinedAt)
            .Select(gm => new GroupDto(
                gm.Group.Id, gm.Group.Name, gm.Group.Description, gm.Group.InviteCode,
                gm.Group.Teacher.FirstName + " " + gm.Group.Teacher.LastName,
                gm.Group.Members.Count, gm.Group.IsActive, gm.Group.CreatedAt))
            .ToListAsync(ct);
    }
}

// Leave Group (Student)
public record LeaveGroupCommand(Guid GroupId, Guid StudentId) : IRequest;

public class LeaveGroupHandler : IRequestHandler<LeaveGroupCommand>
{
    private readonly DbContext _db;
    public LeaveGroupHandler(DbContext db) => _db = db;

    public async Task Handle(LeaveGroupCommand request, CancellationToken ct)
    {
        var member = await _db.Set<GroupMember>()
            .FirstOrDefaultAsync(gm => gm.GroupId == request.GroupId && gm.StudentId == request.StudentId, ct)
            ?? throw new BadRequestException("Ви не є учасником цієї групи");

        _db.Set<GroupMember>().Remove(member);
        await _db.SaveChangesAsync(ct);
    }
}
