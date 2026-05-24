using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TestSystem.Models.Entities;

namespace TestSystem.Data.Data.Configurations;

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("Questions");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Text).HasMaxLength(2000).IsRequired();
        builder.Property(q => q.Explanation).HasMaxLength(2000);
        builder.Property(q => q.DifficultyLevel).HasConversion<string>().HasMaxLength(20);
        builder.Property(q => q.IsActive).HasDefaultValue(true);
        builder.Property(q => q.IsOpenAnswer).HasDefaultValue(false);
        builder.Property(q => q.CorrectAnswerText).HasMaxLength(500);
        builder.Property(q => q.IgnoreCase).HasDefaultValue(true);
        builder.Property(q => q.IgnoreSimilarLetters).HasDefaultValue(true);

        builder.HasOne(q => q.TopicModule)
            .WithMany(t => t.Questions)
            .HasForeignKey(q => q.TopicModuleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
