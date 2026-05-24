namespace TestSystem.Services.DTOs.Subjects;

public record CreateSubjectRequest(string Name, string? Description);
public record UpdateSubjectRequest(string? Name, string? Description);
public record SubjectDto(Guid Id, string Name, string? Description, int TopicCount);
