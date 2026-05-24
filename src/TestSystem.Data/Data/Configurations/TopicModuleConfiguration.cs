using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestSystem.Models.Entities;

namespace TestSystem.Data.Data.Configurations;

public class TopicModuleConfiguration : IEntityTypeConfiguration<TopicModule>
{
    public void Configure(EntityTypeBuilder<TopicModule> builder)
    {
        builder.ToTable("TopicModules");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Title).HasMaxLength(300).IsRequired();

        builder.HasOne(t => t.Subject)
            .WithMany(s => s.TopicModules)
            .HasForeignKey(t => t.SubjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.CreatedByUser)
            .WithMany(u => u.CreatedTopicModules)
            .HasForeignKey(t => t.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
