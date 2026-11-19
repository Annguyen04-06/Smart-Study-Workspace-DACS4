using System.Text;
using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SmartStudy.Application.Common.Interfaces;
using SmartStudy.Application.DTOs.Chat;
using SmartStudy.Application.Validators;
using SmartStudy.Domain.Entities;
using SmartStudy.Domain.Enums;
using SmartStudy.Domain.Exceptions;

namespace SmartStudy.Application.Services;

/// <summary>
/// Hợp đồng dịch vụ Thảo luận Thời gian thực, Ghim kiến thức trọng tâm (IsPinned) & Trợ giảng ảo @bot.
/// </summary>
public interface IChatService
{
    Task<IReadOnlyList<ChatMessageDto>> GetRoomMessagesAsync(
        int roomId,
        int currentUserId,
        bool isAdmin,
        long? beforeId,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delta Sync: lấy các tin nhắn mới hơn afterId (tăng dần) khi điện thoại mở lại App hoặc SignalR vừa kết nối lại.
    /// </summary>
    Task<IReadOnlyList<ChatMessageDto>> GetRoomMessagesAfterAsync(
        int roomId,
        int currentUserId,
        bool isAdmin,
        long afterId,
        int maxCount,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatMessageDto>> GetPinnedMessagesAsync(
        int roomId,
        int currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<ChatMessageDto> TogglePinMessageAsync(
        int roomId,
        long messageId,
        int currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<ChatMessageDto> ToggleMessageReactionAsync(
        int roomId,
        long messageId,
        int currentUserId,
        string emoji,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<ChatMessageDto> SaveStudentMessageAsync(
        int roomId,
        int senderUserId,
        SendMessageRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streaming câu trả lời @bot: gọi onChunk cho từng cụm chữ ngay khi Gemini sinh ra, sau đó lưu câu trả lời hoàn chỉnh
    /// vào ChatMessages + AiUsageLogs. Luồng stream lỗi sẽ tự chuyển sang endpoint không stream để @bot không bao giờ im lặng.
    /// </summary>
    Task<BotReplyResult> StreamAndSaveBotReplyAsync(
        int roomId,
        int triggeredByUserId,
        string questionContent,
        Func<string, Task> onChunk,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Triển khai lưu trữ tin nhắn, phân trang cuộn ngược, Delta Sync, ghim tin nhắn và ngữ cảnh Sliding Window cho @bot.
/// </summary>
public class ChatService : IChatService
{
    private const string BotSenderName = "Trợ giảng AI (@bot)";
    private const string BotBusyAnswer = "⚠️ **Trợ giảng AI đang tạm thời bận xử lý.** Vui lòng gửi lại câu hỏi sau giây lát.";

    // 8 tin trước câu hỏi + chính câu hỏi vừa lưu (AI Service tự bỏ câu hỏi trùng ở cuối và cắt mỗi tin 400 ký tự)
    private const int SlidingWindowMessageCount = 9;
    private const int MaterialExcerptChars = 4000;

    private static readonly HashSet<string> AllowedReactionEmojis = new(StringComparer.Ordinal)
    {
        "🔥", "💡", "👏", "❤️", "🎯", "👍", "🚀"
    };

    private readonly IAppDbContext _context;
    private readonly IRoomService _roomService;
    private readonly IAiMicroserviceClient _aiClient;
    private readonly IValidator<SendMessageRequestDto> _messageValidator;

    public ChatService(
        IAppDbContext context,
        IRoomService roomService,
        IAiMicroserviceClient aiClient,
        IValidator<SendMessageRequestDto> messageValidator)
    {
        _context = context;
        _roomService = roomService;
        _aiClient = aiClient;
        _messageValidator = messageValidator;
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetRoomMessagesAsync(
        int roomId,
        int currentUserId,
        bool isAdmin,
        long? beforeId,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await _roomService.EnsureUserIsRoomMemberAsync(roomId, currentUserId, isAdmin, cancellationToken);

        var clampedSize = Math.Clamp(pageSize, 1, 100);
        var query = _context.ChatMessages
            .AsNoTracking()
            .Where(m => m.RoomId == roomId);

        if (beforeId.HasValue && beforeId.Value > 0)
        {
            query = query.Where(m => m.Id < beforeId.Value);
        }

        var newestFirst = await ProjectToDtosAsync(
            query.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).Take(clampedSize),
            cancellationToken);

        newestFirst.Reverse();
        return newestFirst;
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetRoomMessagesAfterAsync(
        int roomId,
        int currentUserId,
        bool isAdmin,
        long afterId,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        await _roomService.EnsureUserIsRoomMemberAsync(roomId, currentUserId, isAdmin, cancellationToken);

        var query = _context.ChatMessages
            .AsNoTracking()
            .Where(m => m.RoomId == roomId && m.Id > afterId)
            .OrderBy(m => m.Id)
            .Take(Math.Clamp(maxCount, 1, 200));

        return await ProjectToDtosAsync(query, cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetPinnedMessagesAsync(
        int roomId,
        int currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        await _roomService.EnsureUserIsRoomMemberAsync(roomId, currentUserId, isAdmin, cancellationToken);

        var query = _context.ChatMessages
            .AsNoTracking()
            .Where(m => m.RoomId == roomId && m.IsPinned)
            .OrderByDescending(m => m.CreatedAt)
            .Take(10);

        return await ProjectToDtosAsync(query, cancellationToken);
    }

    public async Task<ChatMessageDto> TogglePinMessageAsync(
        int roomId,
        long messageId,
        int currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        await _roomService.EnsureUserIsRoomMemberAsync(roomId, currentUserId, isAdmin, cancellationToken);

        var message = await _context.ChatMessages
            .Include(m => m.Sender)
            .FirstOrDefaultAsync(m => m.Id == messageId && m.RoomId == roomId, cancellationToken)
            ?? throw new NotFoundException("Tin nhắn", messageId);

        message.IsPinned = !message.IsPinned;
        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(message, ParseReactions(message.ReactionsJson));
    }

    public async Task<ChatMessageDto> ToggleMessageReactionAsync(
        int roomId,
        long messageId,
        int currentUserId,
        string emoji,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmoji = (emoji ?? string.Empty).Trim();
        if (!AllowedReactionEmojis.Contains(normalizedEmoji))
        {
            throw new DomainException("Biểu tượng cảm xúc không hợp lệ.");
        }

        await _roomService.EnsureUserIsRoomMemberAsync(roomId, currentUserId, isAdmin, cancellationToken);

        var message = await _context.ChatMessages
            .Include(m => m.Sender)
            .FirstOrDefaultAsync(m => m.Id == messageId && m.RoomId == roomId, cancellationToken)
            ?? throw new NotFoundException("Tin nhắn", messageId);

        var reactions = ParseReactions(message.ReactionsJson);
        if (!reactions.TryGetValue(normalizedEmoji, out var userList))
        {
            userList = new List<int>();
            reactions[normalizedEmoji] = userList;
        }

        if (userList.Contains(currentUserId))
        {
            userList.RemoveAll(id => id == currentUserId);
            if (userList.Count == 0)
            {
                reactions.Remove(normalizedEmoji);
            }
        }
        else
        {
            userList.Add(currentUserId);
        }

        message.ReactionsJson = reactions.Count > 0 ? JsonSerializer.Serialize(reactions) : null;
        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(message, reactions);
    }

    public async Task<ChatMessageDto> SaveStudentMessageAsync(
        int roomId,
        int senderUserId,
        SendMessageRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await ValidationHelper.ValidateOrThrowAsync(_messageValidator, request, cancellationToken);
        await _roomService.EnsureUserIsRoomMemberAsync(roomId, senderUserId, isAdmin: false, cancellationToken);

        var sender = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == senderUserId, cancellationToken)
            ?? throw new NotFoundException("Người dùng", senderUserId);

        var entity = new ChatMessage
        {
            RoomId = roomId,
            SenderId = senderUserId,
            IsAiBot = false,
            IsPinned = false,
            Content = request.Content.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.ChatMessages.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return new ChatMessageDto
        {
            Id = entity.Id,
            RoomId = entity.RoomId,
            SenderId = sender.Id,
            SenderName = sender.FullName,
            SenderAvatarUrl = sender.AvatarUrl,
            IsAiBot = false,
            IsPinned = false,
            Content = entity.Content,
            CreatedAt = entity.CreatedAt
        };
    }

    public async Task<BotReplyResult> StreamAndSaveBotReplyAsync(
        int roomId,
        int triggeredByUserId,
        string questionContent,
        Func<string, Task> onChunk,
        CancellationToken cancellationToken = default)
    {
        var assistantContext = await LoadAssistantContextAsync(roomId, cancellationToken);
        var answerBuilder = new StringBuilder();
        AiTokenUsageInfo? usage = null;
        string? fallbackReason = null;

        try
        {
            await foreach (var streamEvent in _aiClient.StreamRoomAssistantAsync(
                assistantContext.RoomName,
                assistantContext.SubjectCategory,
                questionContent,
                assistantContext.RecentMessages,
                assistantContext.Materials,
                cancellationToken))
            {
                if (streamEvent.CompletedUsage is not null)
                {
                    usage = streamEvent.CompletedUsage;
                    break;
                }

                if (!string.IsNullOrEmpty(streamEvent.Chunk))
                {
                    answerBuilder.Append(streamEvent.Chunk);
                    await onChunk(streamEvent.Chunk);
                }
            }

            if (usage is null || answerBuilder.Length == 0)
            {
                throw new DomainException("Luồng @bot kết thúc nhưng không có nội dung trả lời.");
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Fallback an toàn: gọi lại endpoint không stream; câu trả lời hoàn chỉnh sẽ thay thế phần chữ đã phát dở
            fallbackReason = ex.Message;
            var (fallbackAnswer, fallbackUsage) = await AskWithoutStreamingAsync(assistantContext, questionContent, cancellationToken);
            answerBuilder.Clear().Append(fallbackAnswer);
            usage = fallbackUsage;
        }

        var savedMessage = await SaveBotReplyAsync(roomId, triggeredByUserId, answerBuilder.ToString(), usage, cancellationToken);
        return new BotReplyResult(savedMessage, fallbackReason);
    }

    private async Task<(string Answer, AiTokenUsageInfo Usage)> AskWithoutStreamingAsync(
        BotAssistantContext assistantContext,
        string questionContent,
        CancellationToken cancellationToken)
    {
        try
        {
            var aiResult = await _aiClient.AskRoomAssistantAsync(
                roomName: assistantContext.RoomName,
                subjectCategory: assistantContext.SubjectCategory,
                question: questionContent,
                recentMessages: assistantContext.RecentMessages,
                materialSummaries: assistantContext.Materials,
                cancellationToken: cancellationToken);
            return (aiResult.Answer, aiResult.Usage);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return (BotBusyAnswer, new AiTokenUsageInfo("gemini-2.5-flash", 0, 0, 0, 0, IsSuccess: false));
        }
    }

    private async Task<BotAssistantContext> LoadAssistantContextAsync(int roomId, CancellationToken cancellationToken)
    {
        var room = await _context.StudyRooms.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roomId, cancellationToken)
            ?? throw new NotFoundException("Phòng học", roomId);

        var recentMessages = await _context.ChatMessages
            .AsNoTracking()
            .Where(m => m.RoomId == roomId)
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Take(SlidingWindowMessageCount)
            .Select(m => new AiChatHistoryItem(
                m.IsAiBot ? "Trợ giảng AI" : (m.Sender != null ? m.Sender.FullName : "Sinh viên"),
                m.IsAiBot,
                m.Content,
                m.CreatedAt.ToString("O")))
            .ToListAsync(cancellationToken);
        recentMessages.Reverse();

        var materials = await _context.CourseMaterials
            .AsNoTracking()
            .Where(m => m.RoomId == roomId && m.IsApproved)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new AiMaterialContextItem(
                m.Title,
                m.AiSummary ?? string.Empty,
                m.ExtractedText != null && m.ExtractedText.Length > MaterialExcerptChars
                    ? m.ExtractedText.Substring(0, MaterialExcerptChars)
                    : m.ExtractedText))
            .ToListAsync(cancellationToken);

        return new BotAssistantContext(room.Name, room.SubjectCategory, recentMessages, materials);
    }

    private async Task<ChatMessageDto> SaveBotReplyAsync(
        int roomId,
        int triggeredByUserId,
        string answer,
        AiTokenUsageInfo usage,
        CancellationToken cancellationToken)
    {
        _context.AiUsageLogs.Add(new AiUsageLog
        {
            UserId = triggeredByUserId,
            RoomId = roomId,
            FeatureType = AiFeatureType.BotChat,
            ModelName = usage.ModelName,
            PromptTokens = usage.PromptTokens,
            CompletionTokens = usage.CompletionTokens,
            TotalTokens = usage.TotalTokens,
            DurationMs = usage.DurationMs,
            IsSuccess = usage.IsSuccess,
            CreatedAt = DateTime.UtcNow
        });

        var botMessage = new ChatMessage
        {
            RoomId = roomId,
            SenderId = null,
            IsAiBot = true,
            IsPinned = false,
            Content = answer,
            CreatedAt = DateTime.UtcNow
        };

        _context.ChatMessages.Add(botMessage);
        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(botMessage, new Dictionary<string, List<int>>());
    }

    private static async Task<List<ChatMessageDto>> ProjectToDtosAsync(
        IQueryable<ChatMessage> orderedQuery,
        CancellationToken cancellationToken)
    {
        var rows = await orderedQuery
            .Select(m => new ChatMessageRow(
                m.Id,
                m.RoomId,
                m.SenderId,
                m.IsAiBot ? BotSenderName : (m.Sender != null ? m.Sender.FullName : "Thành viên"),
                m.Sender != null ? m.Sender.AvatarUrl : null,
                m.IsAiBot,
                m.IsPinned,
                m.Content,
                m.ReactionsJson,
                m.CreatedAt))
            .ToListAsync(cancellationToken);

        return rows.Select(r => new ChatMessageDto
        {
            Id = r.Id,
            RoomId = r.RoomId,
            SenderId = r.SenderId,
            SenderName = r.SenderName,
            SenderAvatarUrl = r.SenderAvatarUrl,
            IsAiBot = r.IsAiBot,
            IsPinned = r.IsPinned,
            Content = r.Content,
            Reactions = ParseReactions(r.ReactionsJson),
            CreatedAt = r.CreatedAt
        }).ToList();
    }

    private static ChatMessageDto MapToDto(ChatMessage message, Dictionary<string, List<int>> reactions)
    {
        return new ChatMessageDto
        {
            Id = message.Id,
            RoomId = message.RoomId,
            SenderId = message.SenderId,
            SenderName = message.IsAiBot ? BotSenderName : (message.Sender?.FullName ?? "Thành viên"),
            SenderAvatarUrl = message.IsAiBot ? null : message.Sender?.AvatarUrl,
            IsAiBot = message.IsAiBot,
            IsPinned = message.IsPinned,
            Content = message.Content,
            Reactions = reactions,
            CreatedAt = message.CreatedAt
        };
    }

    private static Dictionary<string, List<int>> ParseReactions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, List<int>>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, List<int>>>(json)
                   ?? new Dictionary<string, List<int>>();
        }
        catch
        {
            return new Dictionary<string, List<int>>();
        }
    }

    private sealed record ChatMessageRow(
        long Id,
        int RoomId,
        int? SenderId,
        string SenderName,
        string? SenderAvatarUrl,
        bool IsAiBot,
        bool IsPinned,
        string Content,
        string? ReactionsJson,
        DateTime CreatedAt);

    private sealed record BotAssistantContext(
        string RoomName,
        string SubjectCategory,
        IReadOnlyList<AiChatHistoryItem> RecentMessages,
        IReadOnlyList<AiMaterialContextItem> Materials);
}
