using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartStudy.Domain.Entities;

namespace SmartStudy.Infrastructure.Persistence.Configurations;

/// <summary>
/// Cấu hình bảng QuizQuestions lưu câu hỏi, 4 lựa chọn NVARCHAR(1000), đáp án CHAR(1) và giải thích AI.
/// </summary>
public class QuizQuestionConfiguration : IEntityTypeConfiguration<QuizQuestion>
{
    public void Configure(EntityTypeBuilder<QuizQuestion> builder)
    {
        builder.ToTable("QuizQuestions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.QuestionText)
            .HasColumnType("nvarchar(max)")
            .IsUnicode(true)
            .IsRequired();

        builder.Property(x => x.OptionA).HasMaxLength(1000).IsUnicode(true).IsRequired();
        builder.Property(x => x.OptionB).HasMaxLength(1000).IsUnicode(true).IsRequired();
        builder.Property(x => x.OptionC).HasMaxLength(1000).IsUnicode(true).IsRequired();
        builder.Property(x => x.OptionD).HasMaxLength(1000).IsUnicode(true).IsRequired();

        builder.Property(x => x.CorrectOption)
            .HasColumnType("char(1)")
            .HasMaxLength(1)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Explanation)
            .HasColumnType("nvarchar(max)")
            .IsUnicode(true)
            .IsRequired();

        builder.HasIndex(x => new { x.QuizId, x.OrderIndex })
            .HasDatabaseName("IX_QuizQuestions_QuizId_OrderIndex");

        builder.HasOne(x => x.Quiz)
            .WithMany(q => q.Questions)
            .HasForeignKey(x => x.QuizId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
