using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartStudy.Domain.Entities;

namespace SmartStudy.Infrastructure.Persistence.Configurations;

/// <summary>
/// Cấu hình bảng QuizAttemptAnswers: QuestionId bắt buộc dùng Restrict để không xung đột Cascade với QuizAttempts.
/// </summary>
public class QuizAttemptAnswerConfiguration : IEntityTypeConfiguration<QuizAttemptAnswer>
{
    public void Configure(EntityTypeBuilder<QuizAttemptAnswer> builder)
    {
        builder.ToTable("QuizAttemptAnswers");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SelectedOption)
            .HasColumnType("char(1)")
            .HasMaxLength(1)
            .IsUnicode(false);

        builder.HasOne(x => x.Attempt)
            .WithMany(a => a.Answers)
            .HasForeignKey(x => x.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Question)
            .WithMany(q => q.AttemptAnswers)
            .HasForeignKey(x => x.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
