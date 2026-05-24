using TestSystem.Models.Common;
using TestSystem.Models.Enums;

namespace TestSystem.Models.Entities;

public class TestSessionAnswer : BaseEntity
{
    public Guid TestSessionId { get; set; }
    public Guid QuestionId { get; set; }
    public Guid? SelectedAnswerOptionId { get; set; }
    /// <summary>
    /// Comma-separated list of selected answer option IDs for multiple choice questions.
    /// </summary>
    public string? SelectedAnswerOptionIds { get; set; }
    public string? TextAnswer { get; set; }
    public bool IsCorrect { get; set; }
    public DifficultyLevel DifficultyAtTime { get; set; }
    public int PointsAwarded { get; set; }

    // Feature 6: Teacher review of open answers
    public OpenAnswerReviewStatus? ReviewStatus { get; set; }
    public string? ReviewFeedback { get; set; }
    public int? OverriddenPoints { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public TestSession TestSession { get; set; } = null!;
    public Question Question { get; set; } = null!;
    public AnswerOption? SelectedAnswerOption { get; set; }
    public User? ReviewedByUser { get; set; }
}
