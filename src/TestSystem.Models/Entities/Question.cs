using TestSystem.Models.Common;
using TestSystem.Models.Enums;

namespace TestSystem.Models.Entities;

public class Question : BaseEntity
{
    public string Text { get; set; } = string.Empty;
    public string? Explanation { get; set; }
    public Guid TopicModuleId { get; set; }
    public DifficultyLevel DifficultyLevel { get; set; }
    public int Points { get; set; }
    public bool IsMultipleChoice { get; set; }
    public bool IsOpenAnswer { get; set; }
    public string? CorrectAnswerText { get; set; }
    public bool IgnoreCase { get; set; } = true;
    public bool IgnoreSimilarLetters { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public TopicModule TopicModule { get; set; } = null!;
    public ICollection<AnswerOption> AnswerOptions { get; set; } = new List<AnswerOption>();
    public ICollection<TestSessionAnswer> TestSessionAnswers { get; set; } = new List<TestSessionAnswer>();
}
