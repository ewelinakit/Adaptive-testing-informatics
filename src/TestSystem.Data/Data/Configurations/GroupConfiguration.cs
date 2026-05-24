using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestSystem.Models.Entities;

namespace TestSystem.Data.Data.Configurations;

public class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("Groups");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Name).HasMaxLength(200).IsRequired();
        builder.Property(g => g.Description).HasMaxLength(500);
        builder.Property(g => g.InviteCode).HasMaxLength(8).IsRequired();
        builder.HasIndex(g => g.InviteCode).IsUnique();
        builder.Property(g => g.IsActive).HasDefaultValue(true);

        builder.HasOne(g => g.Teacher)
            .WithMany(u => u.OwnedGroups)
            .HasForeignKey(g => g.TeacherId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
