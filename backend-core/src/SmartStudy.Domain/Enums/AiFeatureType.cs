namespace SmartStudy.Domain.Enums;

/// <summary>
/// Phân loại tính năng AI phục vụ giám sát lưu lượng Token trên cổng Quản trị viên.
/// </summary>
public enum AiFeatureType
{
    ExtractPdf = 0,
    BotChat = 1,
    GenerateQuiz = 2,
    GenerateFlashcards = 3,
    GenerateMindmap = 4,
    AnalyzeWeakness = 5
}
