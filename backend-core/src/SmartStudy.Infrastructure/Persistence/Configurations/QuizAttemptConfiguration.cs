using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartStudy.Domain.Entities;

namespace SmartStudy.Infrastructure.Persistence.Configurations;

/// <summary>
/// Cấu hình bảng QuizAttempts với điểm số DECIMAL(5,2) và lịch sử hoàn thành theo sinh viên.
/// </summary>
public class QuizAttemptConfiguration : IEntityTypeConfiguration<QuizAttempt>
{
    public void Configure(EntityTypeBuilder<QuizAttempt> builder)
    {
        builder.ToTable("QuizAttempts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Score)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.HasIndex(x => new { x.UserId, x.CompletedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_QuizAttempts_UserId_CompletedAt");

        builder.HasOne(x => x.Quiz)
            .WithMany(q => q.Attempts)
            .HasForeignKey(x => x.QuizId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany(u => u.QuizAttempts)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
