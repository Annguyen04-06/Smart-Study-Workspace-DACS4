using SmartStudy.Domain.Common;

namespace SmartStudy.Domain.Entities;

/// <summary>
/// Thực thể Phương án trả lời chi tiết của từng câu hỏi trong một lượt thi.
/// </summary>
public class QuizAttemptAnswer : BaseEntity<int>
{
    public int AttemptId { get; set; }
    public int QuestionId { get; set; }
    public string? SelectedOption { get; set; }
    public bool IsCorrect { get; set; }

    public QuizAttempt Attempt { get; set; } = null!;
    public QuizQuestion Question { get; set; } = null!;
}
