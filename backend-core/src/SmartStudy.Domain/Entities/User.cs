using SmartStudy.Domain.Common;
using SmartStudy.Domain.Enums;

namespace SmartStudy.Domain.Entities;

/// <summary>
/// Thực thể Người dùng hệ thống (Sinh viên & Quản trị viên).
/// Lưu ý: Admin có StudentCode = null; Sinh viên có mã số sinh viên duy nhất.
/// </summary>
public class User : BaseEntity
{
    public string? StudentCode { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public UserRole Role { get; set; } = UserRole.Student;
    public bool IsLocked { get; set; }

    public ICollection<StudyRoom> CreatedRooms { get; set; } = new List<StudyRoom>();
    public ICollection<RoomMember> RoomMemberships { get; set; } = new List<RoomMember>();
    public ICollection<ChatMessage> SentMessages { get; set; } = new List<ChatMessage>();
    public ICollection<CourseMaterial> UploadedMaterials { get; set; } = new List<CourseMaterial>();
    public ICollection<Quiz> CreatedQuizzes { get; set; } = new List<Quiz>();
    public ICollection<QuizAttempt> QuizAttempts { get; set; } = new List<QuizAttempt>();
    public ICollection<FlashcardDeck> CreatedFlashcardDecks { get; set; } = new List<FlashcardDeck>();
    public ICollection<AiUsageLog> AiUsageLogs { get; set; } = new List<AiUsageLog>();
    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();
}
