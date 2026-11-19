using SmartStudy.Domain.Common;

namespace SmartStudy.Domain.Entities;

/// <summary>
/// Thực thể Bộ thẻ ghi nhớ Flashcard sinh từ nội dung tài liệu học tập PDF.
/// </summary>
public class FlashcardDeck : BaseEntity
{
    public int RoomId { get; set; }
    public int MaterialId { get; set; }
    public int CreatedById { get; set; }
    public string Title { get; set; } = string.Empty;
    public int CardCount { get; set; }

    public StudyRoom Room { get; set; } = null!;
    public CourseMaterial Material { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    public ICollection<Flashcard> Cards { get; set; } = new List<Flashcard>();
}
