namespace SmartStudy.Application.DTOs.Flashcards;

/// <summary>
/// Các DTO quản lý Bộ thẻ Ghi nhớ 3D và trạng thái thuộc bài Spaced Repetition.
/// </summary>
public class GenerateFlashcardDeckRequestDto
{
    public int MaterialId { get; set; }
    public string? Title { get; set; }
    public int CardCount { get; set; } = 8;
}

public class UpdateFlashcardMasteryRequestDto
{
    public int MasteryLevel { get; set; }
}

public class FlashcardDeckSummaryDto
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public int MaterialId { get; set; }
    public string MaterialTitle { get; set; } = string.Empty;
    public int CreatedById { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int CardCount { get; set; }
    public int MasteredCount { get; set; }
    public int LearningCount { get; set; }
    public int UnlearnedCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class FlashcardItemDto
{
    public int Id { get; set; }
    public int DeckId { get; set; }
    public string FrontContent { get; set; } = string.Empty;
    public string BackContent { get; set; } = string.Empty;
    public int MasteryLevel { get; set; }
}

public class FlashcardDeckDetailDto : FlashcardDeckSummaryDto
{
    public IReadOnlyList<FlashcardItemDto> Cards { get; set; } = Array.Empty<FlashcardItemDto>();
}
