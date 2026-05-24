using TestSystem.Models.Common;

namespace TestSystem.Models.Entities;

public class Test : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid GroupId { get; set; }
    public Guid? TopicModuleId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public bool IsActive { get; set; } = true;

    // Feature 1: Time limit
    public int? TimeLimitMinutes { get; set; }

    // Feature 2: Max attempts
    public int? MaxAttempts { get; set; }

    // Feature 3: Shuffle
    public bool ShuffleQuestions { get; set; } = false;
    public bool ShuffleAnswers { get; set; } = false;

    // Show correct answers after test
    public bool ShowCorrectAnswers { get; set; } = true;

    // Feature 4: Schedule availability
    public DateTime? AvailableFrom { get; set; }
    public DateTime? AvailableTo { get; set; }

    public Group Group { get; set; } = null!;
    public TopicModule? TopicModule { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public ICollection<TestQuestion> TestQuestions { get; set; } = new List<TestQuestion>();
    public ICollection<TestSession> TestSessions { get; set; } = new List<TestSession>();
}
