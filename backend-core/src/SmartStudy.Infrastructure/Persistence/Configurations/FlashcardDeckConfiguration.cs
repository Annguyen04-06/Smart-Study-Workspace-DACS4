using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartStudy.Domain.Entities;

namespace SmartStudy.Infrastructure.Persistence.Configurations;

/// <summary>
/// Cấu hình bảng FlashcardDecks: MaterialId dùng NoAction và CreatedById dùng Restrict.
/// </summary>
public class FlashcardDeckConfiguration : IEntityTypeConfiguration<FlashcardDeck>
{
    public void Configure(EntityTypeBuilder<FlashcardDeck> builder)
    {
        builder.ToTable("FlashcardDecks");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsUnicode(true)
            .IsRequired();

        builder.HasIndex(x => new { x.RoomId, x.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_FlashcardDecks_RoomId_CreatedAt");

        builder.HasOne(x => x.Room)
            .WithMany(r => r.FlashcardDecks)
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Material)
            .WithMany(m => m.FlashcardDecks)
            .HasForeignKey(x => x.MaterialId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.CreatedBy)
            .WithMany(u => u.CreatedFlashcardDecks)
            .HasForeignKey(x => x.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
