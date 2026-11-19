using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartStudy.Domain.Entities;

namespace SmartStudy.Infrastructure.Persistence.Configurations;

/// <summary>
/// Cấu hình bảng CourseMaterials lưu trữ siêu dữ liệu PDF và bản tóm tắt học thuật AI.
/// </summary>
public class CourseMaterialConfiguration : IEntityTypeConfiguration<CourseMaterial>
{
    public void Configure(EntityTypeBuilder<CourseMaterial> builder)
    {
        builder.ToTable("CourseMaterials");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsUnicode(true)
            .IsRequired();

        builder.Property(x => x.OriginalFileName)
            .HasMaxLength(255)
            .IsUnicode(true)
            .IsRequired();

        builder.Property(x => x.StoredFilePath)
            .HasMaxLength(500)
            .IsUnicode(true)
            .IsRequired();

        builder.Property(x => x.ExtractedText)
            .HasColumnType("nvarchar(max)")
            .IsUnicode(true);

        builder.Property(x => x.AiSummary)
            .HasColumnType("nvarchar(max)")
            .IsUnicode(true);

        builder.Property(x => x.AiMindmap)
            .HasColumnType("nvarchar(max)")
            .IsUnicode(true);

        builder.Property(x => x.IsApproved)
            .HasDefaultValue(true);

        builder.HasIndex(x => new { x.RoomId, x.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_CourseMaterials_RoomId_CreatedAt");

        builder.HasOne(x => x.Room)
            .WithMany(r => r.Materials)
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.UploadedBy)
            .WithMany(u => u.UploadedMaterials)
            .HasForeignKey(x => x.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
