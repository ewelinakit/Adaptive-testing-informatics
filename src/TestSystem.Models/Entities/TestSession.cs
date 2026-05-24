using TestSystem.Models.Common;
using TestSystem.Models.Enums;

namespace TestSystem.Models.Entities;

public class TestSession : BaseEntity
{
    public Guid StudentId { get; set; }
    public Guid? TopicModuleId { get; set; }
    public Guid? TestId { get; set; }
    public TestSessionStatus Status { get; set; } = TestSessionStatus.InProgress;
    public DifficultyLevel CurrentDifficulty { get; set; } = DifficultyLevel.Easy;
    public int TotalScore { get; set; }
    public int MaxPossibleScore { get; set; }
    public double ScorePercentage { get; set; }
    public int TotalQuestions { get; set; }
    public int CorrectAnswers { get; set; }
    public int ConsecutiveCorrect { get; set; }
    public int ConsecutiveWrong { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // Feature 1: Time limit deadline
    public DateTime? DeadlineAt { get; set; }

    // Feature 5: Auto-abandon inactive sessions
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    public User Student { get; set; } = null!;
    public TopicModule? TopicModule { get; set; }
    public Test? Test { get; set; }
    public ICollection<TestSessionAnswer> Answers { get; set; } = new List<TestSessionAnswer>();
}
