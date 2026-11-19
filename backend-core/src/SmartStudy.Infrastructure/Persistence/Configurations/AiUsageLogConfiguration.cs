using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartStudy.Domain.Entities;

namespace SmartStudy.Infrastructure.Persistence.Configurations;

/// <summary>
/// Cấu hình bảng AiUsageLogs: UserId dùng Restrict, RoomId dùng SetNull để bảo toàn lịch sử Token khi xóa phòng.
/// </summary>
public class AiUsageLogConfiguration : IEntityTypeConfiguration<AiUsageLog>
{
    public void Configure(EntityTypeBuilder<AiUsageLog> builder)
    {
        builder.ToTable("AiUsageLogs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FeatureType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsUnicode(true)
            .IsRequired();

        builder.Property(x => x.ModelName)
            .HasMaxLength(50)
            .IsUnicode(true)
            .IsRequired();

        builder.HasIndex(x => new { x.CreatedAt, x.FeatureType })
            .IsDescending(true, false)
            .HasDatabaseName("IX_AiUsageLogs_CreatedAt_FeatureType");

        builder.HasOne(x => x.User)
            .WithMany(u => u.AiUsageLogs)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Room)
            .WithMany(r => r.AiUsageLogs)
            .HasForeignKey(x => x.RoomId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
