using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartStudy.Domain.Entities;

namespace SmartStudy.Infrastructure.Persistence.Configurations;

/// <summary>
/// Cấu hình bảng StudyRooms với khóa ngoại CreatedById thiết lập DeleteBehavior.Restrict.
/// </summary>
public class StudyRoomConfiguration : IEntityTypeConfiguration<StudyRoom>
{
    public void Configure(EntityTypeBuilder<StudyRoom> builder)
    {
        builder.ToTable("StudyRooms");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(150)
            .IsUnicode(true)
            .IsRequired();

        builder.Property(x => x.SubjectCategory)
            .HasMaxLength(100)
            .IsUnicode(true)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(500)
            .IsUnicode(true);

        builder.Property(x => x.InviteCode)
            .HasMaxLength(10)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(true)
            .IsRequired();

        builder.HasIndex(x => x.InviteCode)
            .IsUnique()
            .HasDatabaseName("IX_StudyRooms_InviteCode");

        builder.HasIndex(x => new { x.Status, x.SubjectCategory, x.CreatedAt })
            .HasDatabaseName("IX_StudyRooms_SubjectCategory_Status");

        builder.HasOne(x => x.CreatedBy)
            .WithMany(u => u.CreatedRooms)
            .HasForeignKey(x => x.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
