using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartStudy.Domain.Entities;

namespace SmartStudy.Infrastructure.Persistence.Configurations;

/// <summary>
/// Cấu hình bảng trung gian RoomMembers kèm Unique Composite Index (RoomId, UserId).
/// </summary>
public class RoomMemberConfiguration : IEntityTypeConfiguration<RoomMember>
{
    public void Configure(EntityTypeBuilder<RoomMember> builder)
    {
        builder.ToTable("RoomMembers");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RoleInRoom)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(true)
            .IsRequired();

        builder.HasIndex(x => new { x.RoomId, x.UserId })
            .IsUnique()
            .HasDatabaseName("IX_RoomMembers_RoomId_UserId");

        builder.HasOne(x => x.Room)
            .WithMany(r => r.Members)
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany(u => u.RoomMemberships)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
