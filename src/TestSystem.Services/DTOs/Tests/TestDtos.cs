namespace TestSystem.Services.DTOs.Tests;

public record CreateTestRequest(
    string Title,
    string? Description,
    Guid GroupId,
    Guid? TopicModuleId,
    List<Guid> QuestionIds,
    int? TimeLimitMinutes = null,
    int? MaxAttempts = null,
    bool ShuffleQuestions = false,
    bool ShuffleAnswers = false,
    DateTime? AvailableFrom = null,
    DateTime? AvailableTo = null,
    bool ShowCorrectAnswers = true);

public record UpdateTestRequest(
    string? Title,
    string? Description,
    bool? IsActive,
    int? TimeLimitMinutes = null,
    int? MaxAttempts = null,
    bool? ShuffleQuestions = null,
    bool? ShuffleAnswers = null,
    DateTime? AvailableFrom = null,
    DateTime? AvailableTo = null,
    bool ClearTimeLimitMinutes = false,
    bool ClearMaxAttempts = false,
    bool ClearAvailableFrom = false,
    bool ClearAvailableTo = false,
    bool? ShowCorrectAnswers = null);
public record AddQuestionsRequest(List<Guid> QuestionIds);

public record TestDto(
    Guid Id,
    string Title,
    string? Description,
    Guid GroupId,
    string GroupName,
    Guid? TopicModuleId,
    string? TopicName,
    List<string> TopicNames,
    int QuestionCount,
    bool IsActive,
    DateTime CreatedAt,
    int? TimeLimitMinutes = null,
    int? MaxAttempts = null,
    bool ShuffleQuestions = false,
    bool ShuffleAnswers = false,
    DateTime? AvailableFrom = null,
    DateTime? AvailableTo = null,
    bool ShowCorrectAnswers = true);

public record AssignedTestDto(
    Guid Id,
    string Title,
    string? Description,
    string GroupName,
    string? TopicName,
    List<string> TopicNames,
    int QuestionCount,
    bool IsActive,
    int? TimeLimitMinutes = null,
    int? MaxAttempts = null,
    int AttemptsUsed = 0,
    DateTime? AvailableFrom = null,
    DateTime? AvailableTo = null);
