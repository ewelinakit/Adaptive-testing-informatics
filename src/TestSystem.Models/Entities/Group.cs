using TestSystem.Models.Common;

namespace TestSystem.Models.Entities;

public class Group : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string InviteCode { get; set; } = string.Empty;
    public Guid TeacherId { get; set; }
    public bool IsActive { get; set; } = true;

    public User Teacher { get; set; } = null!;
    public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
    public ICollection<Test> Tests { get; set; } = new List<Test>();
}
