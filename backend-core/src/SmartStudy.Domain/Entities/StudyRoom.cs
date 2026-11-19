using SmartStudy.Domain.Common;
using SmartStudy.Domain.Enums;

namespace SmartStudy.Domain.Entities;

/// <summary>
/// Thực thể Phòng học nhóm trực tuyến tích hợp kho PDF, thảo luận thời gian thực và AI.
/// </summary>
public class StudyRoom : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string SubjectCategory { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string InviteCode { get; set; } = string.Empty;
    public RoomStatus Status { get; set; } = RoomStatus.Active;
    public int CreatedById { get; set; }

    public User CreatedBy { get; set; } = null!;
    public ICollection<RoomMember> Members { get; set; } = new List<RoomMember>();
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    public ICollection<CourseMaterial> Materials { get; set; } = new List<CourseMaterial>();
    public ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();
    public ICollection<FlashcardDeck> FlashcardDecks { get; set; } = new List<FlashcardDeck>();
    public ICollection<AiUsageLog> AiUsageLogs { get; set; } = new List<AiUsageLog>();
}
