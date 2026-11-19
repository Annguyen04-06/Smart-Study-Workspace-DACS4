using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartStudy.Domain.Entities;

namespace SmartStudy.Infrastructure.Persistence.Configurations;

/// <summary>
/// Cấu hình bảng Users chuẩn 3NF với Filtered Unique Index cho StudentCode và Unique Index cho Email.
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.StudentCode)
            .HasMaxLength(20)
            .IsUnicode(true);

        builder.Property(x => x.FullName)
            .HasMaxLength(100)
            .IsUnicode(true)
            .IsRequired();

        builder.Property(x => x.Email)
            .HasMaxLength(150)
            .IsUnicode(true)
            .IsRequired();

        builder.Property(x => x.PasswordHash)
            .HasMaxLength(255)
            .IsUnicode(true)
            .IsRequired();

        builder.Property(x => x.AvatarUrl)
            .HasMaxLength(500)
            .IsUnicode(true);

        builder.Property(x => x.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(true)
            .IsRequired();

        builder.Property(x => x.IsLocked)
            .HasDefaultValue(false);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => x.Email)
            .IsUnique()
            .HasDatabaseName("IX_Users_Email");

        builder.HasIndex(x => x.StudentCode)
            .IsUnique()
            .HasFilter("[StudentCode] IS NOT NULL")
            .HasDatabaseName("IX_Users_StudentCode");
    }
}
