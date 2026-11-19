using SmartStudy.Domain.Common;

namespace SmartStudy.Domain.Entities;

/// <summary>
/// Thực thể Câu hỏi trắc nghiệm 4 lựa chọn (A/B/C/D) kèm lời giải thích học thuật của AI.
/// </summary>
public class QuizQuestion : BaseEntity<int>
{
    public int QuizId { get; set; }
    public int OrderIndex { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string OptionA { get; set; } = string.Empty;
    public string OptionB { get; set; } = string.Empty;
    public string OptionC { get; set; } = string.Empty;
    public string OptionD { get; set; } = string.Empty;
    public string CorrectOption { get; set; } = "A";
    public string Explanation { get; set; } = string.Empty;

    public Quiz Quiz { get; set; } = null!;
    public ICollection<QuizAttemptAnswer> AttemptAnswers { get; set; } = new List<QuizAttemptAnswer>();
}
