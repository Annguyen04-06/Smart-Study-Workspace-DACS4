using SmartStudy.Domain.Common;

namespace SmartStudy.Domain.Entities;

/// <summary>
/// Thực thể Thẻ học 2 mặt hỗ trợ đánh giá mức độ nhớ Spaced Repetition (0, 1, 2).
/// </summary>
public class Flashcard : BaseEntity<int>
{
    public int DeckId { get; set; }
    public string FrontContent { get; set; } = string.Empty;
    public string BackContent { get; set; } = string.Empty;
    public int MasteryLevel { get; set; }

    public FlashcardDeck Deck { get; set; } = null!;
}
