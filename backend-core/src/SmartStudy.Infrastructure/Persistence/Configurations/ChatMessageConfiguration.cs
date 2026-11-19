using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartStudy.Domain.Entities;

namespace SmartStudy.Infrastructure.Persistence.Configurations;

/// <summary>
/// Cấu hình bảng ChatMessages (PK BIGINT) tối ưu Composite Index trên (RoomId, CreatedAt DESC).
/// </summary>
public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("ChatMessages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Content)
            .HasColumnType("nvarchar(max)")
            .IsUnicode(true)
            .IsRequired();

        builder.Property(x => x.IsAiBot)
            .HasDefaultValue(false);

        builder.Property(x => x.IsPinned)
            .HasDefaultValue(false);

        builder.Property(x => x.ReactionsJson)
            .HasMaxLength(2000)
            .IsUnicode(true)
            .IsRequired(false);

        builder.HasIndex(x => new { x.RoomId, x.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_ChatMessages_RoomId_CreatedAt");

        builder.HasOne(x => x.Room)
            .WithMany(r => r.Messages)
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Sender)
            .WithMany(u => u.SentMessages)
            .HasForeignKey(x => x.SenderId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
