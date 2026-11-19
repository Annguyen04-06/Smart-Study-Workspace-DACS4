using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartStudy.Domain.Entities;

namespace SmartStudy.Infrastructure.Persistence.Configurations;

/// <summary>
/// Cấu hình bảng Quizzes: MaterialId dùng DeleteBehavior.NoAction và CreatedById dùng Restrict
/// để triệt tiêu hoàn toàn lỗi Multiple Cascade Paths trên SQL Server.
/// </summary>
public class QuizConfiguration : IEntityTypeConfiguration<Quiz>
{
    public void Configure(EntityTypeBuilder<Quiz> builder)
    {
        builder.ToTable("Quizzes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsUnicode(true)
            .IsRequired();

        builder.Property(x => x.Difficulty)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(true)
            .IsRequired();

        builder.HasIndex(x => new { x.RoomId, x.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_Quizzes_RoomId_CreatedAt");

        builder.HasOne(x => x.Room)
            .WithMany(r => r.Quizzes)
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Material)
            .WithMany(m => m.Quizzes)
            .HasForeignKey(x => x.MaterialId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.CreatedBy)
            .WithMany(u => u.CreatedQuizzes)
            .HasForeignKey(x => x.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
