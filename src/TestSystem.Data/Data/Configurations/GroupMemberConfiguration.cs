using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestSystem.Models.Entities;

namespace TestSystem.Data.Data.Configurations;

public class GroupMemberConfiguration : IEntityTypeConfiguration<GroupMember>
{
    public void Configure(EntityTypeBuilder<GroupMember> builder)
    {
        builder.ToTable("GroupMembers");
        builder.HasKey(gm => gm.Id);
        builder.HasIndex(gm => new { gm.GroupId, gm.StudentId }).IsUnique();

        builder.HasOne(gm => gm.Group)
            .WithMany(g => g.Members)
            .HasForeignKey(gm => gm.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(gm => gm.Student)
            .WithMany(u => u.GroupMemberships)
            .HasForeignKey(gm => gm.StudentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
