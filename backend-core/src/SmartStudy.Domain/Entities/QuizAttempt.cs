using SmartStudy.Domain.Common;

namespace SmartStudy.Domain.Entities;

/// <summary>
/// Thực thể Lượt làm bài kiểm tra của sinh viên, lưu điểm số thang 10 và thời gian hoàn thành.
/// </summary>
public class QuizAttempt : BaseEntity<int>
{
    public int QuizId { get; set; }
    public int UserId { get; set; }
    public decimal Score { get; set; }
    public int CorrectAnswersCount { get; set; }
    public int TotalQuestions { get; set; }
    public int TimeSpentSeconds { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    public Quiz Quiz { get; set; } = null!;
    public User User { get; set; } = null!;
    public ICollection<QuizAttemptAnswer> Answers { get; set; } = new List<QuizAttemptAnswer>();
}
