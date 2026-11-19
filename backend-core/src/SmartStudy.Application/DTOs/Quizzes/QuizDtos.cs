namespace SmartStudy.Application.DTOs.Quizzes;

/// <summary>
/// Các DTO cho luồng Sinh đề Trắc nghiệm AI, Làm bài có đếm ngược và Chấm điểm thang 10.
/// </summary>
public class GenerateQuizRequestDto
{
    public int MaterialId { get; set; }
    public string? Title { get; set; }
    public int QuestionCount { get; set; } = 5;
    public int DurationMinutes { get; set; } = 10;
    public string Difficulty { get; set; } = "Medium";
}

public class SubmitAnswerItemDto
{
    public int QuestionId { get; set; }
    public string? SelectedOption { get; set; }
}

public class SubmitQuizRequestDto
{
    public int TimeSpentSeconds { get; set; }
    public List<SubmitAnswerItemDto> Answers { get; set; } = new();
}

public class QuizSummaryDto
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public int MaterialId { get; set; }
    public string MaterialTitle { get; set; } = string.Empty;
    public int CreatedById { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int QuestionCount { get; set; }
    public int DurationMinutes { get; set; }
    public string Difficulty { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public decimal? MyBestScore { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class QuizTakeQuestionDto
{
    public int Id { get; set; }
    public int OrderIndex { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string OptionA { get; set; } = string.Empty;
    public string OptionB { get; set; } = string.Empty;
    public string OptionC { get; set; } = string.Empty;
    public string OptionD { get; set; } = string.Empty;
}

public class QuizTakeDto
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public int MaterialId { get; set; }
    public string MaterialTitle { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int QuestionCount { get; set; }
    public int DurationMinutes { get; set; }
    public string Difficulty { get; set; } = string.Empty;
    public IReadOnlyList<QuizTakeQuestionDto> Questions { get; set; } = Array.Empty<QuizTakeQuestionDto>();
}

public class QuizQuestionReviewDto
{
    public int QuestionId { get; set; }
    public int OrderIndex { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string OptionA { get; set; } = string.Empty;
    public string OptionB { get; set; } = string.Empty;
    public string OptionC { get; set; } = string.Empty;
    public string OptionD { get; set; } = string.Empty;
    public string? SelectedOption { get; set; }
    public string CorrectOption { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public string Explanation { get; set; } = string.Empty;
}

public class QuizAttemptResultDto
{
    public int AttemptId { get; set; }
    public int QuizId { get; set; }
    public int RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public string QuizTitle { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public int CorrectAnswersCount { get; set; }
    public int TotalQuestions { get; set; }
    public int TimeSpentSeconds { get; set; }
    public DateTime CompletedAt { get; set; }
    public IReadOnlyList<QuizQuestionReviewDto> Details { get; set; } = Array.Empty<QuizQuestionReviewDto>();
}

public class QuizAttemptHistoryItemDto
{
    public int AttemptId { get; set; }
    public int QuizId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public int RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public int CorrectAnswersCount { get; set; }
    public int TotalQuestions { get; set; }
    public int TimeSpentSeconds { get; set; }
    public DateTime CompletedAt { get; set; }
}

/// <summary>
/// Yêu cầu phân tích lỗ hổng kiến thức AI (có thể lọc theo phòng học cụ thể hoặc tổng hợp toàn bộ môn).
/// </summary>
public class AnalyzeWeaknessRequestDto
{
    public int? RoomId { get; set; }
}

public class TopicStrengthDto
{
    public string SubjectCategory { get; set; } = string.Empty;
    public int RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public int TotalQuestions { get; set; }
    public int CorrectCount { get; set; }
    public int WrongCount { get; set; }
    public double AccuracyPercent { get; set; }
}

public class WeaknessGapDto
{
    public string TopicName { get; set; } = string.Empty;
    public string SubjectCategory { get; set; } = string.Empty;
    public int MistakeCount { get; set; }
    public string Severity { get; set; } = "Medium";
    public string RootCause { get; set; } = string.Empty;
    public string StudyAction { get; set; } = string.Empty;
    public string RecommendedPrompt { get; set; } = string.Empty;
}

public class WeaknessAnalysisResultDto
{
    public DateTime AnalyzedAt { get; set; }
    public int TotalAttemptsAnalyzed { get; set; }
    public int TotalQuestionsAnswered { get; set; }
    public int TotalWrongAnswers { get; set; }
    public double OverallAccuracyPercent { get; set; }
    public string OverallDiagnosis { get; set; } = string.Empty;
    public string StrongestSubject { get; set; } = string.Empty;
    public string PrioritySubject { get; set; } = string.Empty;
    public IReadOnlyList<TopicStrengthDto> SubjectRadar { get; set; } = Array.Empty<TopicStrengthDto>();
    public IReadOnlyList<WeaknessGapDto> TopWeaknesses { get; set; } = Array.Empty<WeaknessGapDto>();
    public IReadOnlyList<string> StudyRoadmap { get; set; } = Array.Empty<string>();
    public int? SuggestedRoomId { get; set; }
    public int? SuggestedMaterialId { get; set; }
    public string? SuggestedRoomName { get; set; }
}

/// <summary>
/// Yêu cầu sinh nhanh đề Quiz 5 câu vá lỗ hổng tập trung vào các chủ đề sinh viên hay làm sai.
/// </summary>
public class GenerateRemedialQuizRequestDto
{
    public int? RoomId { get; set; }
    public int? MaterialId { get; set; }
    public List<string>? FocusTopics { get; set; }
    public int QuestionCount { get; set; } = 5;
}
