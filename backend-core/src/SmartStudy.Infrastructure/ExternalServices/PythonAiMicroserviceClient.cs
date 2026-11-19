using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartStudy.Application.Common.Interfaces;
using SmartStudy.Domain.Exceptions;

namespace SmartStudy.Infrastructure.ExternalServices;

/// <summary>
/// Cấu hình kết nối nội bộ sang Python FastAPI Microservice (Port 8000).
/// </summary>
public class AiMicroserviceSettings
{
    public const string SectionName = "AiMicroservice";

    // 127.0.0.1 thay vì "localhost": Windows thử IPv6 ::1 trước và mất ~2 giây mỗi kết nối mới khi AI chỉ nghe IPv4
    public string BaseUrl { get; set; } = "http://127.0.0.1:8000";
    public string InternalApiKey { get; set; } = "smartstudy-internal-secret-key-2026";
    public int TimeoutSeconds { get; set; } = 90;
}

/// <summary>
/// Typed HttpClient gọi sang Python FastAPI (đính kèm X-Internal-Api-Key, đồng bộ snake_case JSON và xử lý Timeout/Fallback).
/// Trợ giảng @bot đọc luồng Server-Sent Events bằng HttpCompletionOption.ResponseHeadersRead để nhận từng cụm chữ tức thì.
/// </summary>
public class PythonAiMicroserviceClient : IAiMicroserviceClient
{
    private static readonly JsonSerializerOptions SnakeCaseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<PythonAiMicroserviceClient> _logger;
    private readonly TimeSpan _streamTimeout;

    public PythonAiMicroserviceClient(
        HttpClient httpClient,
        IOptions<AiMicroserviceSettings> options,
        ILogger<PythonAiMicroserviceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var cfg = options.Value;
        _httpClient.BaseAddress = new Uri(cfg.BaseUrl.TrimEnd('/') + "/");
        _streamTimeout = TimeSpan.FromSeconds(Math.Max(15, cfg.TimeoutSeconds));
        _httpClient.Timeout = _streamTimeout;
        _httpClient.DefaultRequestHeaders.Remove("X-Internal-Api-Key");
        _httpClient.DefaultRequestHeaders.Add("X-Internal-Api-Key", cfg.InternalApiKey);
    }

    public async Task<AiRuntimeStatusResponse> GetRuntimeStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("api/v1/ai/config-status", cancellationToken);
            var envelope = await ReadEnvelopeAsync<FastApiConfigStatusData>(response, cancellationToken);
            return new AiRuntimeStatusResponse(
                envelope.Data!.GeminiConfigured,
                envelope.Data.ModelName,
                envelope.Data.MaskedKey,
                envelope.Data.ActiveMode);
        }
        catch
        {
            return new AiRuntimeStatusResponse(false, "gemini-2.5-flash", null, "offline");
        }
    }

    public async Task<AiRuntimeStatusResponse> UpdateGeminiApiKeyAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendJsonPostAsync(
            "api/v1/ai/config-key",
            new FastApiUpdateKeyRequest(apiKey),
            cancellationToken);
        var envelope = await ReadEnvelopeAsync<FastApiConfigStatusData>(response, cancellationToken);
        return new AiRuntimeStatusResponse(
            envelope.Data!.GeminiConfigured,
            envelope.Data.ModelName,
            envelope.Data.MaskedKey,
            envelope.Data.ActiveMode);
    }

    public async Task<AiPdfExtractionResponse> ExtractPdfAsync(
        Stream pdfStream,
        string fileName,
        string title,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            var streamContent = new StreamContent(pdfStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(streamContent, "file", fileName);
            form.Add(new StringContent(title), "title");

            using var response = await _httpClient.PostAsync("api/v1/ai/extract-pdf", form, cancellationToken);
            var envelope = await ReadEnvelopeAsync<FastApiPdfData>(response, cancellationToken);

            return new AiPdfExtractionResponse(
                PageCount: Math.Max(1, envelope.Data!.PageCount),
                ExtractedText: envelope.Data.ExtractedText ?? string.Empty,
                AiSummary: envelope.Data.AiSummary ?? string.Empty,
                Usage: MapUsage(envelope.Usage, isSuccess: true));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or DomainException)
        {
            // Cơ chế Fallback an toàn: Vẫn cho phép lưu và đọc trực tiếp PDF trên trình duyệt ngay cả khi dịch vụ tóm tắt đang bận
            _logger.LogWarning(ex, "Dịch vụ tóm tắt AI tạm bận khi xử lý PDF '{FileName}'. Sử dụng chế độ lưu trữ trực tiếp.", fileName);
            return new AiPdfExtractionResponse(
                PageCount: 1,
                ExtractedText: string.Empty,
                AiSummary: $"*Tài liệu **{title}** đã được tải lên thành công. Hệ thống đang bận nên chưa tạo bản tóm tắt tự động; bạn vẫn có thể mở đọc trực tiếp tệp PDF ở khung bên cạnh.*",
                Usage: new AiTokenUsageInfo("gemini-2.5-flash", 0, 0, 0, 0, IsSuccess: false));
        }
    }

    public async Task<AiAssistantAnswerResponse> AskRoomAssistantAsync(
        string roomName,
        string subjectCategory,
        string question,
        IReadOnlyList<AiChatHistoryItem> recentMessages,
        IReadOnlyList<AiMaterialContextItem> materialSummaries,
        CancellationToken cancellationToken = default)
    {
        var payload = BuildChatPayload(roomName, subjectCategory, question, recentMessages, materialSummaries);

        using var response = await SendJsonPostAsync("api/v1/ai/chat-assistant", payload, cancellationToken);
        var envelope = await ReadEnvelopeAsync<FastApiAssistantData>(response, cancellationToken);

        return new AiAssistantAnswerResponse(
            Answer: envelope.Data!.Answer,
            Usage: MapUsage(envelope.Usage, isSuccess: true));
    }

    public async IAsyncEnumerable<AiAssistantStreamEvent> StreamRoomAssistantAsync(
        string roomName,
        string subjectCategory,
        string question,
        IReadOnlyList<AiChatHistoryItem> recentMessages,
        IReadOnlyList<AiMaterialContextItem> materialSummaries,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var payload = BuildChatPayload(roomName, subjectCategory, question, recentMessages, materialSummaries);

        // HttpClient.Timeout chỉ bao phủ tới lúc nhận Header (ResponseHeadersRead) => tự giới hạn tổng thời gian đọc luồng
        using var streamTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        streamTimeout.CancelAfter(_streamTimeout);

        using var response = await OpenEventStreamAsync("api/v1/ai/chat-assistant-stream", payload, streamTimeout.Token);
        await using var body = await response.Content.ReadAsStreamAsync(streamTimeout.Token);
        using var reader = new StreamReader(body, Encoding.UTF8);

        // Đọc từng dòng "data: {...}" của Server-Sent Events và trả ngay từng cụm chữ cho tầng nghiệp vụ
        while (await reader.ReadLineAsync(streamTimeout.Token) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var streamEvent = JsonSerializer.Deserialize<FastApiStreamEvent>(line.AsSpan(5).Trim(), SnakeCaseJsonOptions);
            switch (streamEvent?.Type)
            {
                case "chunk" when !string.IsNullOrEmpty(streamEvent.Text):
                    yield return AiAssistantStreamEvent.FromChunk(streamEvent.Text);
                    break;
                case "done":
                    yield return AiAssistantStreamEvent.Completed(new AiTokenUsageInfo(
                        string.IsNullOrWhiteSpace(streamEvent.ModelName) ? "gemini-2.5-flash" : streamEvent.ModelName,
                        streamEvent.PromptTokens,
                        streamEvent.CompletionTokens,
                        streamEvent.TotalTokens,
                        streamEvent.DurationMs,
                        IsSuccess: true));
                    yield break;
                case "error":
                    throw new DomainException(streamEvent.Message ?? "AI Microservice báo lỗi trong luồng phản hồi @bot.");
            }
        }

        throw new DomainException("Luồng phản hồi @bot bị ngắt trước khi hoàn tất.");
    }

    private static FastApiChatRequest BuildChatPayload(
        string roomName,
        string subjectCategory,
        string question,
        IReadOnlyList<AiChatHistoryItem> recentMessages,
        IReadOnlyList<AiMaterialContextItem> materialSummaries)
    {
        return new FastApiChatRequest(
            RoomName: roomName,
            SubjectCategory: subjectCategory,
            Question: question,
            RecentMessages: recentMessages.Select(m => new FastApiChatHistoryItem(
                m.SenderName,
                m.IsAiBot,
                m.Content,
                m.CreatedAt)).ToList(),
            MaterialSummaries: materialSummaries.Select(m => new FastApiMaterialItem(
                m.Title,
                m.Summary,
                m.ExtractedExcerpt)).ToList());
    }

    private async Task<HttpResponseMessage> OpenEventStreamAsync<TPayload>(
        string relativeUrl,
        TPayload payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, relativeUrl)
        {
            Content = JsonContent.Create(payload, options: SnakeCaseJsonOptions)
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (TaskCanceledException ex)
        {
            throw new DomainException("Hệ thống xử lý AI đang quá tải, vui lòng thử lại sau giây lát.", new[] { ex.Message });
        }
        catch (HttpRequestException ex)
        {
            throw new DomainException("Dịch vụ xử lý AI tạm thời gián đoạn, vui lòng thử lại sau.", new[] { ex.Message });
        }

        if (!response.IsSuccessStatusCode)
        {
            var statusCode = (int)response.StatusCode;
            response.Dispose();
            throw new DomainException($"AI Microservice trả về mã trạng thái HTTP {statusCode} cho luồng @bot.");
        }

        return response;
    }

    public async Task<AiGeneratedQuizResponse> GenerateQuizAsync(
        string materialTitle,
        string extractedText,
        string? aiSummary,
        int questionCount,
        string difficulty,
        CancellationToken cancellationToken = default)
    {
        var payload = new FastApiGenerateQuizRequest(
            MaterialTitle: materialTitle,
            ExtractedText: extractedText,
            AiSummary: aiSummary,
            QuestionCount: questionCount,
            Difficulty: difficulty);

        using var response = await SendJsonPostAsync("api/v1/ai/generate-quiz", payload, cancellationToken);
        var envelope = await ReadEnvelopeAsync<FastApiQuizData>(response, cancellationToken);

        var questions = envelope.Data!.Questions.Select(q => new AiGeneratedQuestionItem(
            q.QuestionText,
            q.OptionA,
            q.OptionB,
            q.OptionC,
            q.OptionD,
            q.CorrectOption,
            q.Explanation)).ToList();

        return new AiGeneratedQuizResponse(
            SuggestedTitle: envelope.Data.SuggestedTitle,
            Questions: questions,
            Usage: MapUsage(envelope.Usage, isSuccess: true));
    }

    public async Task<AiGeneratedFlashcardDeckResponse> GenerateFlashcardsAsync(
        string materialTitle,
        string extractedText,
        string? aiSummary,
        int cardCount,
        CancellationToken cancellationToken = default)
    {
        var payload = new FastApiGenerateFlashcardsRequest(
            MaterialTitle: materialTitle,
            ExtractedText: extractedText,
            AiSummary: aiSummary,
            CardCount: cardCount);

        using var response = await SendJsonPostAsync("api/v1/ai/generate-flashcards", payload, cancellationToken);
        var envelope = await ReadEnvelopeAsync<FastApiFlashcardData>(response, cancellationToken);

        var cards = envelope.Data!.Cards.Select(c => new AiGeneratedFlashcardItem(
            c.FrontContent,
            c.BackContent)).ToList();

        return new AiGeneratedFlashcardDeckResponse(
            SuggestedTitle: envelope.Data.SuggestedTitle,
            Cards: cards,
            Usage: MapUsage(envelope.Usage, isSuccess: true));
    }

    public async Task<AiGeneratedMindmapResponse> GenerateMindmapAsync(
        string materialTitle,
        string extractedText,
        string? aiSummary,
        CancellationToken cancellationToken = default)
    {
        var payload = new FastApiGenerateMindmapRequest(
            MaterialTitle: materialTitle,
            ExtractedText: extractedText,
            AiSummary: aiSummary);

        using var response = await SendJsonPostAsync("api/v1/ai/generate-mindmap", payload, cancellationToken);
        var envelope = await ReadEnvelopeAsync<FastApiMindmapData>(response, cancellationToken);

        return new AiGeneratedMindmapResponse(
            Title: envelope.Data!.Title,
            MermaidCode: envelope.Data.MermaidCode,
            NodeCount: envelope.Data.NodeCount,
            Usage: MapUsage(envelope.Usage, isSuccess: true));
    }

    public async Task<AiWeaknessAnalysisResponse> AnalyzeWeaknessAsync(
        string studentName,
        IReadOnlyList<AiSubjectStatInput> subjectStats,
        IReadOnlyList<AiWrongAnswerInput> wrongAnswers,
        CancellationToken cancellationToken = default)
    {
        var payload = new FastApiAnalyzeWeaknessRequest(
            StudentName: studentName,
            SubjectStats: subjectStats.Select(s => new FastApiSubjectStatItem(
                s.SubjectCategory,
                s.RoomName,
                s.TotalAnswered,
                s.CorrectCount,
                s.AccuracyPercent)).ToList(),
            WrongAnswers: wrongAnswers.Select(w => new FastApiWrongAnswerItem(
                w.SubjectCategory,
                w.RoomName,
                w.QuizTitle,
                w.QuestionText,
                w.SelectedOption,
                w.CorrectOption,
                w.Explanation)).ToList());

        using var response = await SendJsonPostAsync("api/v1/ai/analyze-weakness", payload, cancellationToken);
        var envelope = await ReadEnvelopeAsync<FastApiWeaknessAnalysisData>(response, cancellationToken);

        var gaps = envelope.Data!.WeaknessGaps.Select(g => new AiWeaknessGapItem(
            g.TopicName,
            g.SubjectCategory,
            g.MistakeCount,
            g.Severity,
            g.RootCause,
            g.StudyAction,
            g.RecommendedPrompt)).ToList();

        return new AiWeaknessAnalysisResponse(
            OverallDiagnosis: envelope.Data.OverallDiagnosis,
            StrongestSubject: envelope.Data.StrongestSubject,
            PrioritySubject: envelope.Data.PrioritySubject,
            WeaknessGaps: gaps,
            StudyRoadmap: envelope.Data.StudyRoadmap,
            Usage: MapUsage(envelope.Usage, isSuccess: true));
    }

    private async Task<HttpResponseMessage> SendJsonPostAsync<TPayload>(
        string relativeUrl,
        TPayload payload,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.PostAsJsonAsync(relativeUrl, payload, SnakeCaseJsonOptions, cancellationToken);
        }
        catch (TaskCanceledException ex)
        {
            throw new DomainException("Hệ thống xử lý AI đang quá tải, vui lòng thử lại sau giây lát.", new[] { ex.Message });
        }
        catch (HttpRequestException ex)
        {
            throw new DomainException("Dịch vụ xử lý AI tạm thời gián đoạn, vui lòng thử lại sau.", new[] { ex.Message });
        }
    }

    private static async Task<FastApiEnvelope<TData>> ReadEnvelopeAsync<TData>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        FastApiEnvelope<TData>? envelope = null;
        try
        {
            envelope = await response.Content.ReadFromJsonAsync<FastApiEnvelope<TData>>(SnakeCaseJsonOptions, cancellationToken);
        }
        catch
        {
            // Bỏ qua lỗi parse nếu phản hồi không phải JSON
        }

        if (!response.IsSuccessStatusCode || envelope is null || !envelope.Success || envelope.Data is null)
        {
            var reason = envelope?.Message ?? $"AI Microservice trả về mã trạng thái HTTP {(int)response.StatusCode}.";
            throw new DomainException(reason);
        }

        return envelope;
    }

    private static AiTokenUsageInfo MapUsage(FastApiUsage? usage, bool isSuccess)
    {
        if (usage is null)
        {
            return new AiTokenUsageInfo("gemini-2.5-flash", 0, 0, 0, 0, isSuccess);
        }

        return new AiTokenUsageInfo(
            string.IsNullOrWhiteSpace(usage.ModelName) ? "gemini-2.5-flash" : usage.ModelName,
            usage.PromptTokens,
            usage.CompletionTokens,
            usage.TotalTokens,
            usage.DurationMs,
            isSuccess);
    }

    private sealed record FastApiUsage(
        string ModelName,
        int PromptTokens,
        int CompletionTokens,
        int TotalTokens,
        int DurationMs);

    private sealed record FastApiEnvelope<T>(
        bool Success,
        string Message,
        T? Data,
        FastApiUsage? Usage);

    private sealed record FastApiPdfData(
        int PageCount,
        string? ExtractedText,
        string? AiSummary);

    private sealed record FastApiChatHistoryItem(
        string SenderName,
        bool IsAiBot,
        string Content,
        string? CreatedAt);

    private sealed record FastApiMaterialItem(
        string Title,
        string Summary,
        string? ExtractedExcerpt);

    private sealed record FastApiChatRequest(
        string RoomName,
        string SubjectCategory,
        string Question,
        List<FastApiChatHistoryItem> RecentMessages,
        List<FastApiMaterialItem> MaterialSummaries);

    private sealed record FastApiAssistantData(string Answer);

    private sealed record FastApiStreamEvent(
        string Type,
        string? Text,
        string? Message,
        string? ModelName,
        int PromptTokens,
        int CompletionTokens,
        int TotalTokens,
        int DurationMs);

    private sealed record FastApiGenerateQuizRequest(
        string MaterialTitle,
        string ExtractedText,
        string? AiSummary,
        int QuestionCount,
        string Difficulty);

    private sealed record FastApiQuizQuestionItem(
        string QuestionText,
        string OptionA,
        string OptionB,
        string OptionC,
        string OptionD,
        string CorrectOption,
        string Explanation);

    private sealed record FastApiQuizData(
        string SuggestedTitle,
        List<FastApiQuizQuestionItem> Questions);

    private sealed record FastApiGenerateFlashcardsRequest(
        string MaterialTitle,
        string ExtractedText,
        string? AiSummary,
        int CardCount);

    private sealed record FastApiFlashcardItem(
        string FrontContent,
        string BackContent);

    private sealed record FastApiFlashcardData(
        string SuggestedTitle,
        List<FastApiFlashcardItem> Cards);

    private sealed record FastApiGenerateMindmapRequest(
        string MaterialTitle,
        string ExtractedText,
        string? AiSummary);

    private sealed record FastApiMindmapData(
        string Title,
        string MermaidCode,
        int NodeCount);

    private sealed record FastApiUpdateKeyRequest(string ApiKey);

    private sealed record FastApiConfigStatusData(
        bool GeminiConfigured,
        string ModelName,
        string? MaskedKey,
        string ActiveMode);

    private sealed record FastApiSubjectStatItem(
        string SubjectCategory,
        string RoomName,
        int TotalAnswered,
        int CorrectCount,
        double AccuracyPercent);

    private sealed record FastApiWrongAnswerItem(
        string SubjectCategory,
        string RoomName,
        string QuizTitle,
        string QuestionText,
        string? SelectedOption,
        string CorrectOption,
        string Explanation);

    private sealed record FastApiAnalyzeWeaknessRequest(
        string StudentName,
        List<FastApiSubjectStatItem> SubjectStats,
        List<FastApiWrongAnswerItem> WrongAnswers);

    private sealed record FastApiWeaknessGapItem(
        string TopicName,
        string SubjectCategory,
        int MistakeCount,
        string Severity,
        string RootCause,
        string StudyAction,
        string RecommendedPrompt);

    private sealed record FastApiWeaknessAnalysisData(
        string OverallDiagnosis,
        string StrongestSubject,
        string PrioritySubject,
        List<FastApiWeaknessGapItem> WeaknessGaps,
        List<string> StudyRoadmap);
}
