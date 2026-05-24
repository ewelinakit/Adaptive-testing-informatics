namespace TestSystem.Services.DTOs.Topics;

public record CreateTopicRequest(string Title, Guid SubjectId, int OrderIndex);
public record UpdateTopicRequest(string? Title, Guid? SubjectId, int? OrderIndex);
public record TopicModuleDto(Guid Id, string Title, Guid SubjectId, string SubjectName, int OrderIndex, int QuestionCount, Guid CreatedByUserId, DateTime CreatedAt);
