using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using SmartStudy.Application.Common.Interfaces;
using SmartStudy.Domain.Exceptions;
using SmartStudy.Infrastructure.Persistence;

namespace SmartStudy.UnitTests.TestInfrastructure;

/// <summary>
/// Bộ công cụ khởi tạo InMemory DbContext độc lập và các Stub hạ tầng cho kiểm thử đơn vị (xUnit).
/// </summary>
public static class TestDbContextFactory
{
    public static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"SmartStudyTestDb_{Guid.NewGuid():N}")
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}

public sealed class FakeRealtimeNotifier : IStudyRoomRealtimeNotifier
{
    public List<(int RoomId, string ResourceType, string Title)> Notifications { get; } = new();

    public Task NotifyWorkspaceResourceUpdatedAsync(
        int roomId,
        string resourceType,
        string resourceTitle,
        string actorName,
        CancellationToken cancellationToken = default)
    {
        Notifications.Add((roomId, resourceType, resourceTitle));
        return Task.CompletedTask;
    }
}

public sealed class FakeFileStorageService : IFileStorageService
{
    public Task<string> SaveMaterialPdfAsync(Stream fileStream, string originalFileName, CancellationToken cancellationToken = default)
        => Task.FromResult($"/uploads/materials/test_{originalFileName}");

    public Task<string> SaveAvatarImageAsync(Stream fileStream, string originalFileName, CancellationToken cancellationToken = default)
        => Task.FromResult($"/uploads/avatars/test_{originalFileName}");

    public Task DeleteFileIfExistsAsync(string? storedRelativePath, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public sealed class FakeEmailSender : IEmailSender
{
    public string? LastSentOtpCode { get; private set; }
    public string? LastRecipientEmail { get; private set; }

    public Task<EmailDeliveryResult> SendPasswordResetOtpAsync(
        string recipientEmail,
        string recipientName,
        string otpCode,
        CancellationToken cancellationToken = default)
    {
        LastRecipientEmail = recipientEmail;
        LastSentOtpCode = otpCode;
        return Task.FromResult(new EmailDeliveryResult(true, "Đã gửi qua Fake SMTP."));
    }
}

public sealed class FakeAiMicroserviceClient : IAiMicroserviceClient
{
    public const string NonStreamingAnswer = "Câu trả lời học thuật từ trợ giảng @bot.";

    /// <summary>Các cụm chữ giả lập Gemini phát ra trong luồng Streaming @bot.</summary>
    public IReadOnlyList<string> StreamChunks { get; set; } = ["**Dijkstra** ", "dùng hàng đợi ", "ưu tiên."];

    /// <summary>Bật để giả lập luồng SSE bị ngắt giữa chừng (sau khi đã phát các cụm chữ).</summary>
    public bool BreakStreamMidway { get; set; }

    public async IAsyncEnumerable<AiAssistantStreamEvent> StreamRoomAssistantAsync(
        string roomName,
        string subjectCategory,
        string question,
        IReadOnlyList<AiChatHistoryItem> recentMessages,
        IReadOnlyList<AiMaterialContextItem> materialSummaries,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var chunk in StreamChunks)
        {
            await Task.Yield();
            yield return AiAssistantStreamEvent.FromChunk(chunk);
        }

        if (BreakStreamMidway)
        {
            throw new DomainException("Luồng phản hồi @bot bị ngắt trước khi hoàn tất.");
        }

        yield return AiAssistantStreamEvent.Completed(new AiTokenUsageInfo("gemini-2.5-flash", 300, 120, 420, 640, true));
    }

    public Task<AiRuntimeStatusResponse> GetRuntimeStatusAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new AiRuntimeStatusResponse(true, "gemini-2.5-flash", "AIza••••1234", "gemini-cloud"));

    public Task<AiRuntimeStatusResponse> UpdateGeminiApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
        => Task.FromResult(new AiRuntimeStatusResponse(true, "gemini-2.5-flash", "AIza••••5678", "gemini-cloud"));

    public Task<AiPdfExtractionResponse> ExtractPdfAsync(Stream pdfStream, string fileName, string title, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new AiPdfExtractionResponse(
            PageCount: 5,
            ExtractedText: "Nội dung bài giảng thuật toán Dijkstra và BFS.",
            AiSummary: "### Tóm tắt bài giảng",
            Usage: new AiTokenUsageInfo("gemini-2.5-flash", 200, 100, 300, 850, true)));
    }

    public Task<AiAssistantAnswerResponse> AskRoomAssistantAsync(
        string roomName,
        string subjectCategory,
        string question,
        IReadOnlyList<AiChatHistoryItem> recentMessages,
        IReadOnlyList<AiMaterialContextItem> materialSummaries,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new AiAssistantAnswerResponse(
            Answer: NonStreamingAnswer,
            Usage: new AiTokenUsageInfo("gemini-2.5-flash", 320, 180, 500, 920, true)));
    }

    public Task<AiGeneratedQuizResponse> GenerateQuizAsync(
        string materialTitle,
        string extractedText,
        string? aiSummary,
        int questionCount,
        string difficulty,
        CancellationToken cancellationToken = default)
    {
        var questions = Enumerable.Range(1, questionCount)
            .Select(i => new AiGeneratedQuestionItem(
                QuestionText: $"Câu hỏi kiểm tra số {i}?",
                OptionA: "Đáp án A",
                OptionB: "Đáp án B",
                OptionC: "Đáp án C",
                OptionD: "Đáp án D",
                CorrectOption: "B",
                Explanation: $"Giải thích học thuật cho câu {i}."))
            .ToList();

        return Task.FromResult(new AiGeneratedQuizResponse(
            SuggestedTitle: $"Đề trắc nghiệm tự động: {materialTitle}",
            Questions: questions,
            Usage: new AiTokenUsageInfo("gemini-2.5-flash", 450, 350, 800, 1400, true)));
    }

    public Task<AiGeneratedFlashcardDeckResponse> GenerateFlashcardsAsync(
        string materialTitle,
        string extractedText,
        string? aiSummary,
        int cardCount,
        CancellationToken cancellationToken = default)
    {
        var cards = Enumerable.Range(1, cardCount)
            .Select(i => new AiGeneratedFlashcardItem($"Mặt trước thẻ {i}", $"Mặt sau thẻ {i}"))
            .ToList();

        return Task.FromResult(new AiGeneratedFlashcardDeckResponse(
            SuggestedTitle: $"Bộ thẻ ghi nhớ: {materialTitle}",
            Cards: cards,
            Usage: new AiTokenUsageInfo("gemini-2.5-flash", 300, 250, 550, 1100, true)));
    }

    public Task<AiGeneratedMindmapResponse> GenerateMindmapAsync(
        string materialTitle,
        string extractedText,
        string? aiSummary,
        CancellationToken cancellationToken = default)
    {
        var mermaid = $"flowchart TD\n    root[\"📚 {materialTitle}\"]\n    b1[\"1. Trọng tâm\"]\n    root --> b1";
        return Task.FromResult(new AiGeneratedMindmapResponse(
            Title: $"Sơ đồ Tư duy: {materialTitle}",
            MermaidCode: mermaid,
            NodeCount: 6,
            Usage: new AiTokenUsageInfo("gemini-2.5-flash", 280, 190, 470, 950, true)));
    }

    public Task<AiWeaknessAnalysisResponse> AnalyzeWeaknessAsync(
        string studentName,
        IReadOnlyList<AiSubjectStatInput> subjectStats,
        IReadOnlyList<AiWrongAnswerInput> wrongAnswers,
        CancellationToken cancellationToken = default)
    {
        var priority = subjectStats.OrderBy(s => s.AccuracyPercent).FirstOrDefault()?.SubjectCategory ?? "Khoa học Máy tính";
        var strongest = subjectStats.OrderByDescending(s => s.AccuracyPercent).FirstOrDefault()?.SubjectCategory ?? priority;
        var gaps = new List<AiWeaknessGapItem>
        {
            new(
                TopicName: "Độ phức tạp thuật toán đồ thị",
                SubjectCategory: priority,
                MistakeCount: Math.Max(1, wrongAnswers.Count),
                Severity: "High",
                RootCause: "Nhầm lẫn giữa Binary Heap và mảng kề trong Dijkstra.",
                StudyAction: "Ôn lại chương Đồ thị và làm đề vá lỗ hổng 5 câu.",
                RecommendedPrompt: "@bot Giải thích độ phức tạp thuật toán Dijkstra?")
        };

        return Task.FromResult(new AiWeaknessAnalysisResponse(
            OverallDiagnosis: $"Sinh viên {studentName} cần củng cố mảng {priority}.",
            StrongestSubject: strongest,
            PrioritySubject: priority,
            WeaknessGaps: gaps,
            StudyRoadmap: new[] { "Bước 1: Xem lại lời giải sai", "Bước 2: Làm đề Quiz 5 câu vá lỗ hổng" },
            Usage: new AiTokenUsageInfo("gemini-2.5-flash", 310, 220, 530, 880, true)));
    }
}
