using TestSystem.Models.Common;

namespace TestSystem.Models.Entities;

public class Subject : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<TopicModule> TopicModules { get; set; } = new List<TopicModule>();
}
