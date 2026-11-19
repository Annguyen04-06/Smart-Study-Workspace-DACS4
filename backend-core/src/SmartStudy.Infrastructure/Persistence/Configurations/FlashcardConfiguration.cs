using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartStudy.Domain.Entities;

namespace SmartStudy.Infrastructure.Persistence.Configurations;

/// <summary>
/// Cấu hình bảng Flashcards lưu 2 mặt thẻ và mức độ thuộc bài MasteryLevel (0..2).
/// </summary>
public class FlashcardConfiguration : IEntityTypeConfiguration<Flashcard>
{
    public void Configure(EntityTypeBuilder<Flashcard> builder)
    {
        builder.ToTable("Flashcards");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FrontContent)
            .HasMaxLength(1000)
            .IsUnicode(true)
            .IsRequired();

        builder.Property(x => x.BackContent)
            .HasMaxLength(2000)
            .IsUnicode(true)
            .IsRequired();

        builder.Property(x => x.MasteryLevel)
            .HasDefaultValue(0);

        builder.HasOne(x => x.Deck)
            .WithMany(d => d.Cards)
            .HasForeignKey(x => x.DeckId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
