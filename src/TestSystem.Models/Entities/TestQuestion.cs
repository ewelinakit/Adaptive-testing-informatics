namespace TestSystem.Models.Entities;

public class TestQuestion
{
    public Guid TestId { get; set; }
    public Guid QuestionId { get; set; }
    public int OrderIndex { get; set; }

    public Test Test { get; set; } = null!;
    public Question Question { get; set; } = null!;
}
