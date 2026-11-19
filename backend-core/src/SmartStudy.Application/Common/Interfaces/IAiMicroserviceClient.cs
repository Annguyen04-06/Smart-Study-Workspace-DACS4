namespace SmartStudy.Application.Common.Interfaces;

/// <summary>
/// Dữ liệu đo lường Token trả về từ Python FastAPI sau mỗi lần gọi Google Gemini.
/// </summary>
public record AiTokenUsageInfo(
    string ModelName,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    int DurationMs,
    bool IsSuccess
);

public record AiPdfExtractionResponse(
    int PageCount,
    string ExtractedText,
    string AiSummary,
    AiTokenUsageInfo Usage
);

public record AiChatHistoryItem(
    string SenderName,
    bool IsAiBot,
    string Content,
    string? CreatedAt
);

public record AiMaterialContextItem(
    string Title,
    string Summary,
    string? ExtractedExcerpt
);

public record AiAssistantAnswerResponse(
    string Answer,
    AiTokenUsageInfo Usage
);

/// <summary>
/// 1 sự kiện trong luồng Streaming @bot: hoặc 1 cụm chữ mới (Chunk), hoặc sự kiện kết thúc mang thông số Token (CompletedUsage).
/// </summary>
public record AiAssistantStreamEvent(
    string? Chunk,
    AiTokenUsageInfo? CompletedUsage
)
{
    public static AiAssistantStreamEvent FromChunk(string chunk) => new(chunk, null);

    public static AiAssistantStreamEvent Completed(AiTokenUsageInfo usage) => new(null, usage);
}

public record AiGeneratedQuestionItem(
    string QuestionText,
    string OptionA,
    string OptionB,
    string OptionC,
    string OptionD,
    string CorrectOption,
    string Explanation
);

public record AiGeneratedQuizResponse(
    string SuggestedTitle,
    IReadOnlyList<AiGeneratedQuestionItem> Questions,
    AiTokenUsageInfo Usage
);

public record AiGeneratedFlashcardItem(
    string FrontContent,
    string BackContent
);

public record AiGeneratedFlashcardDeckResponse(
    string SuggestedTitle,
    IReadOnlyList<AiGeneratedFlashcardItem> Cards,
    AiTokenUsageInfo Usage
);

public record AiGeneratedMindmapResponse(
    string Title,
    string MermaidCode,
    int NodeCount,
    AiTokenUsageInfo Usage
);

public record AiRuntimeStatusResponse(
    bool GeminiConfigured,
    string ModelName,
    string? MaskedKey,
    string ActiveMode
);

public record AiSubjectStatInput(
    string SubjectCategory,
    string RoomName,
    int TotalAnswered,
    int CorrectCount,
    double AccuracyPercent
);

public record AiWrongAnswerInput(
    string SubjectCategory,
    string RoomName,
    string QuizTitle,
    string QuestionText,
    string? SelectedOption,
    string CorrectOption,
    string Explanation
);

public record AiWeaknessGapItem(
    string TopicName,
    string SubjectCategory,
    int MistakeCount,
    string Severity,
    string RootCause,
    string StudyAction,
    string RecommendedPrompt
);

public record AiWeaknessAnalysisResponse(
    string OverallDiagnosis,
    string StrongestSubject,
    string PrioritySubject,
    IReadOnlyList<AiWeaknessGapItem> WeaknessGaps,
    IReadOnlyList<string> StudyRoadmap,
    AiTokenUsageInfo Usage
);

/// <summary>
/// Hợp đồng giao tiếp HTTP nội bộ sang tầng AI Microservice (FastAPI - Port 8000).
/// </summary>
public interface IAiMicroserviceClient
{
    Task<AiRuntimeStatusResponse> GetRuntimeStatusAsync(CancellationToken cancellationToken = default);

    Task<AiRuntimeStatusResponse> UpdateGeminiApiKeyAsync(
        string apiKey,
        CancellationToken cancellationToken = default);

    Task<AiPdfExtractionResponse> ExtractPdfAsync(
        Stream pdfStream,
        string fileName,
        string title,
        CancellationToken cancellationToken = default);

    Task<AiAssistantAnswerResponse> AskRoomAssistantAsync(
        string roomName,
        string subjectCategory,
        string question,
        IReadOnlyList<AiChatHistoryItem> recentMessages,
        IReadOnlyList<AiMaterialContextItem> materialSummaries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streaming thật câu trả lời @bot (Server-Sent Events từ Python): trả về từng cụm chữ ngay khi Gemini sinh ra,
    /// phần tử cuối cùng mang CompletedUsage. Ném ngoại lệ nếu luồng lỗi để tầng nghiệp vụ chuyển sang bản không stream.
    /// </summary>
    IAsyncEnumerable<AiAssistantStreamEvent> StreamRoomAssistantAsync(
        string roomName,
        string subjectCategory,
        string question,
        IReadOnlyList<AiChatHistoryItem> recentMessages,
        IReadOnlyList<AiMaterialContextItem> materialSummaries,
        CancellationToken cancellationToken = default);

    Task<AiGeneratedQuizResponse> GenerateQuizAsync(
        string materialTitle,
        string extractedText,
        string? aiSummary,
        int questionCount,
        string difficulty,
        CancellationToken cancellationToken = default);

    Task<AiGeneratedFlashcardDeckResponse> GenerateFlashcardsAsync(
        string materialTitle,
        string extractedText,
        string? aiSummary,
        int cardCount,
        CancellationToken cancellationToken = default);

    Task<AiGeneratedMindmapResponse> GenerateMindmapAsync(
        string materialTitle,
        string extractedText,
        string? aiSummary,
        CancellationToken cancellationToken = default);

    Task<AiWeaknessAnalysisResponse> AnalyzeWeaknessAsync(
        string studentName,
        IReadOnlyList<AiSubjectStatInput> subjectStats,
        IReadOnlyList<AiWrongAnswerInput> wrongAnswers,
        CancellationToken cancellationToken = default);
}
