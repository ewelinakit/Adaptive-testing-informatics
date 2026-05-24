namespace TestSystem.Services.DTOs.Groups;

public record CreateGroupRequest(string Name, string? Description);
public record UpdateGroupRequest(string? Name, string? Description, bool? IsActive);

public record GroupDto(
    Guid Id,
    string Name,
    string? Description,
    string InviteCode,
    string TeacherName,
    int MemberCount,
    bool IsActive,
    DateTime CreatedAt);

public record GroupMemberDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string Email,
    DateTime JoinedAt);

public record JoinGroupRequest(string InviteCode);
