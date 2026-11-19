using SmartStudy.Domain.Common;
using SmartStudy.Domain.Enums;

namespace SmartStudy.Domain.Entities;

/// <summary>
/// Thực thể Đề thi trắc nghiệm sinh tự động từ nội dung một tài liệu PDF cụ thể.
/// </summary>
public class Quiz : BaseEntity
{
    public int RoomId { get; set; }
    public int MaterialId { get; set; }
    public int CreatedById { get; set; }
    public string Title { get; set; } = string.Empty;
    public int QuestionCount { get; set; }
    public int DurationMinutes { get; set; }
    public QuizDifficulty Difficulty { get; set; } = QuizDifficulty.Medium;

    public StudyRoom Room { get; set; } = null!;
    public CourseMaterial Material { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    public ICollection<QuizQuestion> Questions { get; set; } = new List<QuizQuestion>();
    public ICollection<QuizAttempt> Attempts { get; set; } = new List<QuizAttempt>();
}
