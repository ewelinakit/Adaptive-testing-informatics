using TestSystem.Models.Common;

namespace TestSystem.Models.Entities;

public class TopicModule : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public Guid SubjectId { get; set; }
    public int OrderIndex { get; set; }
    public Guid CreatedByUserId { get; set; }

    public Subject Subject { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public ICollection<Question> Questions { get; set; } = new List<Question>();
    public ICollection<TestSession> TestSessions { get; set; } = new List<TestSession>();
}
