using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestSystem.Models.Entities;

namespace TestSystem.Data.Data.Configurations;

public class TestSessionConfiguration : IEntityTypeConfiguration<TestSession>
{
    public void Configure(EntityTypeBuilder<TestSession> builder)
    {
        builder.ToTable("TestSessions");
        builder.HasKey(ts => ts.Id);
        builder.Property(ts => ts.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(ts => ts.CurrentDifficulty).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(ts => ts.Student)
            .WithMany(u => u.TestSessions)
            .HasForeignKey(ts => ts.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ts => ts.TopicModule)
            .WithMany(t => t.TestSessions)
            .HasForeignKey(ts => ts.TopicModuleId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(ts => ts.Test)
            .WithMany(t => t.TestSessions)
            .HasForeignKey(ts => ts.TestId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
