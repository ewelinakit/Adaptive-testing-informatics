using TestSystem.Models.Enums;

namespace TestSystem.Services.DTOs.Questions;

public record CreateQuestionRequest(
    string Text,
    string? Explanation,
    Guid TopicModuleId,
    DifficultyLevel DifficultyLevel,
    int? Points,
    bool IsOpenAnswer = false,
    string? CorrectAnswerText = null,
    bool IgnoreCase = true,
    bool IgnoreSimilarLetters = true,
    List<CreateAnswerOptionRequest>? AnswerOptions = null);

public record CreateAnswerOptionRequest(string Text, bool IsCorrect, int OrderIndex);

public record UpdateQuestionRequest(
    string? Text,
    string? Explanation,
    DifficultyLevel? DifficultyLevel,
    int? Points,
    bool? IsActive,
    bool? IsOpenAnswer = null,
    string? CorrectAnswerText = null,
    bool? IgnoreCase = null,
    bool? IgnoreSimilarLetters = null,
    List<UpdateAnswerOptionRequest>? AnswerOptions = null);

public record UpdateAnswerOptionRequest(Guid? Id, string Text, bool IsCorrect, int OrderIndex);

public record QuestionDto(
    Guid Id,
    string Text,
    string? Explanation,
    Guid TopicModuleId,
    string TopicTitle,
    string DifficultyLevel,
    int Points,
    bool IsMultipleChoice,
    bool IsOpenAnswer,
    string? CorrectAnswerText,
    bool IgnoreCase,
    bool IgnoreSimilarLetters,
    bool IsActive,
    List<AnswerOptionDto> AnswerOptions);

public record AnswerOptionDto(Guid Id, string Text, bool IsCorrect, int OrderIndex);

public record BulkCreateQuestionsRequest(List<CreateQuestionRequest> Questions);
