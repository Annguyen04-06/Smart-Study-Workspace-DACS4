using SmartStudy.Domain.Common;

namespace SmartStudy.Domain.Entities;

/// <summary>
/// Thực thể Học liệu PDF lưu trữ đường dẫn tệp vật lý, toàn văn trích xuất và tóm tắt AI.
/// </summary>
public class CourseMaterial : BaseEntity
{
    public int RoomId { get; set; }
    public int UploadedById { get; set; }
    public string Title { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int PageCount { get; set; } = 1;
    public string? ExtractedText { get; set; }
    public string? AiSummary { get; set; }
    public string? AiMindmap { get; set; }
    public bool IsApproved { get; set; } = true;

    public StudyRoom Room { get; set; } = null!;
    public User UploadedBy { get; set; } = null!;
    public ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();
    public ICollection<FlashcardDeck> FlashcardDecks { get; set; } = new List<FlashcardDeck>();
}
