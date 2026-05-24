namespace TestSystem.Services.DTOs.Testing;

public record StartTestRequest(Guid TestId);

public record TestSessionDto(
    Guid Id,
    Guid? TopicModuleId,
    string TopicTitle,
    Guid? TestId,
    string? TestTitle,
    string Status,
    string CurrentDifficulty,
    int TotalScore,
    int MaxPossibleScore,
    double ScorePercentage,
    int TotalQuestions,
    int CorrectAnswers,
    DateTime StartedAt,
    DateTime? CompletedAt,
    DateTime? DeadlineAt = null);

public record TestQuestionDto(
    Guid QuestionId,
    string Text,
    string CurrentDifficulty,
    int QuestionNumber,
    int MaxQuestions,
    bool IsMultipleChoice,
    bool IsOpenAnswer,
    List<TestAnswerOptionDto> Options);

public record TestAnswerOptionDto(Guid Id, string Text, int OrderIndex);

public record SubmitAnswerRequest(Guid QuestionId, List<Guid>? SelectedAnswerOptionIds = null, string? TextAnswer = null);

public record SubmitAnswerResponse(
    bool IsCorrect,
    int PointsAwarded,
    string? Explanation,
    string NewDifficulty,
    int TotalScore,
    int QuestionNumber,
    bool IsFinished);

public record TestResultDto(
    Guid Id,
    string TopicTitle,
    string SubjectName,
    int TotalScore,
    int MaxPossibleScore,
    double ScorePercentage,
    int TotalQuestions,
    int CorrectAnswers,
    DateTime StartedAt,
    DateTime? CompletedAt,
    List<TestAnswerDetailDto> Answers,
    bool ShowCorrectAnswers = true);

public record TestAnswerDetailDto(
    Guid QuestionId,
    string QuestionText,
    string? Explanation,
    List<Guid> SelectedOptionIds,
    List<string> SelectedOptionTexts,
    List<string> CorrectOptionTexts,
    bool IsCorrect,
    string Difficulty,
    int PointsAwarded,
    bool IsOpenAnswer = false,
    string? TextAnswer = null,
    string? CorrectAnswerText = null,
    string? ReviewStatus = null,
    string? ReviewFeedback = null,
    int? OverriddenPoints = null);

// Feature 6: Review DTOs
public record ReviewAnswerRequest(string Status, string? Feedback, int? Points);

public record PendingReviewDto(
    Guid AnswerId,
    Guid SessionId,
    string StudentName,
    string TestTitle,
    string QuestionText,
    string? TextAnswer,
    int PointsAwarded,
    int MaxPoints,
    DateTime AnsweredAt);

public record ReviewedAnswerDto(
    Guid AnswerId,
    string Status,
    string? Feedback,
    int PointsAwarded);
