using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestSystem.Models.Entities;

namespace TestSystem.Data.Data.Configurations;

public class TestSessionAnswerConfiguration : IEntityTypeConfiguration<TestSessionAnswer>
{
    public void Configure(EntityTypeBuilder<TestSessionAnswer> builder)
    {
        builder.ToTable("TestSessionAnswers");
        builder.HasKey(tsa => tsa.Id);
        builder.Property(tsa => tsa.DifficultyAtTime).HasConversion<string>().HasMaxLength(20);
        builder.Property(tsa => tsa.TextAnswer).HasMaxLength(1000);

        builder.HasOne(tsa => tsa.TestSession)
            .WithMany(ts => ts.Answers)
            .HasForeignKey(tsa => tsa.TestSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tsa => tsa.Question)
            .WithMany(q => q.TestSessionAnswers)
            .HasForeignKey(tsa => tsa.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(tsa => tsa.SelectedAnswerOption)
            .WithMany()
            .HasForeignKey(tsa => tsa.SelectedAnswerOptionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
