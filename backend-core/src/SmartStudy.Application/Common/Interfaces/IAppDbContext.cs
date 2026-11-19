using Microsoft.EntityFrameworkCore;
using SmartStudy.Domain.Entities;

namespace SmartStudy.Application.Common.Interfaces;

/// <summary>
/// Giao diện trừu tượng hóa DbContext theo nguyên lý Dependency Inversion (DIP).
/// Giúp tầng Application truy vấn dữ liệu mà không phụ thuộc trực tiếp vào SQL Server hay Infrastructure.
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<StudyRoom> StudyRooms { get; }
    DbSet<RoomMember> RoomMembers { get; }
    DbSet<ChatMessage> ChatMessages { get; }
    DbSet<CourseMaterial> CourseMaterials { get; }
    DbSet<Quiz> Quizzes { get; }
    DbSet<QuizQuestion> QuizQuestions { get; }
    DbSet<QuizAttempt> QuizAttempts { get; }
    DbSet<QuizAttemptAnswer> QuizAttemptAnswers { get; }
    DbSet<FlashcardDeck> FlashcardDecks { get; }
    DbSet<Flashcard> Flashcards { get; }
    DbSet<AiUsageLog> AiUsageLogs { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
