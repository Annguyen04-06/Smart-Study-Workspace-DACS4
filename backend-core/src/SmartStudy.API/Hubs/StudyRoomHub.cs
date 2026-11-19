using System.Diagnostics;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SmartStudy.API.Services;
using SmartStudy.Application.DTOs.Chat;
using SmartStudy.Application.Services;
using SmartStudy.Domain.Exceptions;

namespace SmartStudy.API.Hubs;

/// <summary>
/// SignalR Hub trung tâm quản lý không gian phòng học thời gian thực (/hubs/study-room):
/// - Quản lý Presence (OnlineUsersUpdated)
/// - Đồng bộ Đồng hồ Pomodoro Học Nhóm (StartPomodoro / PausePomodoro / ResetPomodoro -> PomodoroStateUpdated)
/// - Bảng trắng Học nhóm Trực tiếp & Con trỏ Figma (SendDrawStroke / SendCursorMove / ClearWhiteboard)
/// - Live Floating Emoji Burst & Cảm xúc dưới tin nhắn (SendRoomReaction / ToggleMessageReaction)
/// - Ghim kiến thức trọng tâm tức thì (TogglePinMessage -> MessagePinUpdated)
/// - Phát tín hiệu đang gõ phím độ trễ thấp (SendTypingStatus -> UserTyping)
/// - Thảo luận nhóm (Optimistic UI qua clientMessageId) & Trợ giảng @bot Streaming thật:
///   BotStreamStart (hiện bong bóng ngay) -> BotStreamChunk (gom 50ms) -> BotStreamComplete (toàn văn + id đã lưu),
///   chạy nền an toàn qua IServiceScopeFactory và luôn phát BotThinking(false) ở finally.
/// - Đẩy ServerVersion ngay khi client kết nối/kết nối lại để PWA đồng bộ OTA tức thì sau khi server build bản mới.
/// </summary>
[Authorize]
public class StudyRoomHub : Hub
{
    private static readonly HashSet<string> AllowedBurstEmojis = new(StringComparer.Ordinal)
    {
        "🔥", "💡", "👏", "❤️", "🎯", "👍", "🚀"
    };

    private static readonly TimeSpan BotStreamBatchInterval = TimeSpan.FromMilliseconds(50);
    private static readonly Regex ClientMessageIdPattern = new("^[A-Za-z0-9_-]{1,64}$", RegexOptions.Compiled);

    private readonly PresenceTracker _presenceTracker;
    private readonly RoomPomodoroTracker _pomodoroTracker;
    private readonly RoomWhiteboardTracker _whiteboardTracker;
    private readonly IRoomService _roomService;
    private readonly IChatService _chatService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<StudyRoomHub> _hubContext;
    private readonly AppVersionProvider _versionProvider;
    private readonly ILogger<StudyRoomHub> _logger;

    public StudyRoomHub(
        PresenceTracker presenceTracker,
        RoomPomodoroTracker pomodoroTracker,
        RoomWhiteboardTracker whiteboardTracker,
        IRoomService roomService,
        IChatService chatService,
        IServiceScopeFactory scopeFactory,
        IHubContext<StudyRoomHub> hubContext,
        AppVersionProvider versionProvider,
        ILogger<StudyRoomHub> logger)
    {
        _presenceTracker = presenceTracker;
        _pomodoroTracker = pomodoroTracker;
        _whiteboardTracker = whiteboardTracker;
        _roomService = roomService;
        _chatService = chatService;
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _versionProvider = versionProvider;
        _logger = logger;
    }

    public static string GetRoomGroupName(int roomId) => $"room-{roomId}";

    public override async Task OnConnectedAsync()
    {
        // OTA qua SignalR: client so sánh với phiên bản đang cài để đồng bộ ngay, không cần chờ chu kỳ kiểm tra 60 giây
        var buildInfo = _versionProvider.GetCurrentVersion();
        await Clients.Caller.SendAsync("ServerVersion", new
        {
            version = buildInfo.Version,
            updatedAt = buildInfo.UpdatedAt
        });
        await base.OnConnectedAsync();
    }

    public async Task JoinRoom(int roomId)
    {
        var (userId, fullName, avatarUrl, isAdmin) = GetCallerIdentity();
        await _roomService.EnsureUserIsRoomMemberAsync(roomId, userId, isAdmin);

        var groupName = GetRoomGroupName(roomId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        var onlineUsers = _presenceTracker.UserConnectedToRoom(
            roomId,
            userId,
            fullName,
            avatarUrl,
            Context.ConnectionId);

        await Clients.Group(groupName).SendAsync("OnlineUsersUpdated", new
        {
            roomId,
            onlineUsers
        });

        // Ghi nhớ quyền thành viên trên phiên kết nối để tối ưu tốc độ O(1) cho con trỏ & nét vẽ 35ms
        Context.Items[$"joined:{roomId}"] = true;

        // Đồng bộ ngay trạng thái đồng hồ Pomodoro và Bảng trắng hiện tại của phòng cho tab vừa mở
        var pomodoroState = _pomodoroTracker.GetState(roomId);
        await Clients.Caller.SendAsync("PomodoroStateUpdated", pomodoroState);

        var whiteboardStrokes = _whiteboardTracker.GetStrokes(roomId);
        await Clients.Caller.SendAsync("WhiteboardStateSynced", new
        {
            roomId,
            strokes = whiteboardStrokes
        });
    }

    public async Task LeaveRoom(int roomId)
    {
        var (userId, _, _, _) = GetCallerIdentity();
        var groupName = GetRoomGroupName(roomId);

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        var onlineUsers = _presenceTracker.UserLeftRoom(roomId, userId, Context.ConnectionId);

        await Clients.Group(groupName).SendAsync("OnlineUsersUpdated", new
        {
            roomId,
            onlineUsers
        });
    }

    public async Task StartPomodoro(int roomId, int modeMinutes)
    {
        var (userId, fullName, _, isAdmin) = GetCallerIdentity();
        await _roomService.EnsureUserIsRoomMemberAsync(roomId, userId, isAdmin);

        var state = _pomodoroTracker.Start(roomId, modeMinutes, fullName);
        await Clients.Group(GetRoomGroupName(roomId)).SendAsync("PomodoroStateUpdated", state);
    }

    public async Task PausePomodoro(int roomId)
    {
        var (userId, fullName, _, isAdmin) = GetCallerIdentity();
        await _roomService.EnsureUserIsRoomMemberAsync(roomId, userId, isAdmin);

        var state = _pomodoroTracker.Pause(roomId, fullName);
        await Clients.Group(GetRoomGroupName(roomId)).SendAsync("PomodoroStateUpdated", state);
    }

    public async Task ResetPomodoro(int roomId, int modeMinutes = 25)
    {
        var (userId, fullName, _, isAdmin) = GetCallerIdentity();
        await _roomService.EnsureUserIsRoomMemberAsync(roomId, userId, isAdmin);

        var state = _pomodoroTracker.Reset(roomId, modeMinutes, fullName);
        await Clients.Group(GetRoomGroupName(roomId)).SendAsync("PomodoroStateUpdated", state);
    }

    public async Task TogglePinMessage(int roomId, long messageId)
    {
        var (userId, fullName, _, isAdmin) = GetCallerIdentity();
        var updatedMsg = await _chatService.TogglePinMessageAsync(roomId, messageId, userId, isAdmin);

        await Clients.Group(GetRoomGroupName(roomId)).SendAsync("MessagePinUpdated", new
        {
            roomId,
            actorName = fullName,
            message = updatedMsg
        });
    }

    public async Task SendRoomReaction(int roomId, string emoji)
    {
        var (userId, fullName, _, isAdmin) = GetCallerIdentity();
        await EnsureAuthorizedRoomSessionAsync(roomId, userId, isAdmin);

        var normalized = (emoji ?? string.Empty).Trim();
        if (!AllowedBurstEmojis.Contains(normalized))
        {
            return;
        }

        await Clients.Group(GetRoomGroupName(roomId)).SendAsync("ReceiveRoomReaction", new
        {
            roomId,
            userId,
            fullName,
            emoji = normalized
        });
    }

    public async Task ToggleMessageReaction(int roomId, long messageId, string emoji)
    {
        var (userId, fullName, _, isAdmin) = GetCallerIdentity();
        var updatedMsg = await _chatService.ToggleMessageReactionAsync(roomId, messageId, userId, emoji, isAdmin);

        await Clients.Group(GetRoomGroupName(roomId)).SendAsync("MessageReactionsUpdated", new
        {
            roomId,
            actorName = fullName,
            emoji = (emoji ?? string.Empty).Trim(),
            message = updatedMsg
        });
    }

    public async Task RequestWhiteboardState(int roomId)
    {
        var (userId, _, _, isAdmin) = GetCallerIdentity();
        await EnsureAuthorizedRoomSessionAsync(roomId, userId, isAdmin);

        var strokes = _whiteboardTracker.GetStrokes(roomId);
        await Clients.Caller.SendAsync("WhiteboardStateSynced", new
        {
            roomId,
            strokes
        });
    }

    public async Task SendDrawStroke(int roomId, WhiteboardStrokeDto strokeData)
    {
        if (strokeData is null || strokeData.Points is null || strokeData.Points.Count == 0)
        {
            return;
        }

        var (userId, fullName, _, isAdmin) = GetCallerIdentity();
        await EnsureAuthorizedRoomSessionAsync(roomId, userId, isAdmin);

        strokeData.AuthorUserId = userId;
        strokeData.AuthorName = fullName;
        if (string.IsNullOrWhiteSpace(strokeData.Id))
        {
            strokeData.Id = Guid.NewGuid().ToString("N")[..12];
        }
        if (strokeData.Points.Count > 450)
        {
            strokeData.Points = strokeData.Points.Take(450).ToList();
        }
        strokeData.Width = Math.Clamp(strokeData.Width, 1, 32);

        _whiteboardTracker.AppendStroke(roomId, strokeData);

        await Clients.OthersInGroup(GetRoomGroupName(roomId)).SendAsync("ReceiveDrawStroke", new
        {
            roomId,
            stroke = strokeData
        });
    }

    public async Task SendCursorMove(int roomId, double x, double y)
    {
        var (userId, fullName, _, isAdmin) = GetCallerIdentity();
        await EnsureAuthorizedRoomSessionAsync(roomId, userId, isAdmin);

        var clampedX = Math.Clamp(x, 0.0, 1.0);
        var clampedY = Math.Clamp(y, 0.0, 1.0);

        await Clients.OthersInGroup(GetRoomGroupName(roomId)).SendAsync("ReceiveCursorMove", new
        {
            roomId,
            userId,
            fullName,
            color = RoomWhiteboardTracker.GetUserCursorColor(userId),
            x = clampedX,
            y = clampedY
        });
    }

    public async Task ClearWhiteboard(int roomId)
    {
        var (userId, fullName, _, isAdmin) = GetCallerIdentity();
        await EnsureAuthorizedRoomSessionAsync(roomId, userId, isAdmin);

        _whiteboardTracker.ClearRoom(roomId);

        await Clients.Group(GetRoomGroupName(roomId)).SendAsync("WhiteboardCleared", new
        {
            roomId,
            clearedByUserId = userId,
            clearedByName = fullName
        });
    }

    public async Task SendTypingStatus(int roomId, bool isTyping)
    {
        var (userId, fullName, _, _) = GetCallerIdentity();
        var groupName = GetRoomGroupName(roomId);

        await Clients.OthersInGroup(groupName).SendAsync("UserTyping", new
        {
            roomId,
            userId,
            fullName,
            isTyping
        });
    }

    /// <summary>
    /// Giữ tương thích phiên bản giao diện cũ (chưa có Optimistic UI).
    /// </summary>
    public Task SendMessage(int roomId, string content) => SendChatMessage(roomId, content, null);

    /// <summary>
    /// Gửi tin nhắn kèm clientMessageId để trình duyệt khớp bong bóng "đang gửi" với tin nhắn thật đã lưu.
    /// Trả về tin nhắn đã lưu cho chính người gửi (kết quả invoke) đồng thời phát ReceiveMessage cho cả phòng.
    /// </summary>
    public async Task<ChatMessageDto?> SendChatMessage(int roomId, string content, string? clientMessageId)
    {
        var (userId, fullName, _, _) = GetCallerIdentity();
        var trimmed = (content ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        var savedStudentMessage = await _chatService.SaveStudentMessageAsync(
            roomId,
            userId,
            new SendMessageRequestDto { Content = trimmed });
        savedStudentMessage.ClientMessageId = clientMessageId is not null && ClientMessageIdPattern.IsMatch(clientMessageId)
            ? clientMessageId
            : null;

        var groupName = GetRoomGroupName(roomId);
        await Clients.Group(groupName).SendAsync("ReceiveMessage", savedStudentMessage);

        if (trimmed.StartsWith("@bot", StringComparison.OrdinalIgnoreCase))
        {
            await StartBotReplyAsync(roomId, userId, fullName, trimmed);
        }

        return savedStudentMessage;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var rawId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(rawId, out var userId))
        {
            var affectedRooms = _presenceTracker.ConnectionClosed(userId, Context.ConnectionId);
            foreach (var (roomId, onlineUsers) in affectedRooms)
            {
                await Clients.Group(GetRoomGroupName(roomId)).SendAsync("OnlineUsersUpdated", new
                {
                    roomId,
                    onlineUsers
                });
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task StartBotReplyAsync(int roomId, int triggeredByUserId, string triggeredByName, string questionContent)
    {
        var groupName = GetRoomGroupName(roomId);
        var tempId = $"bot-{Guid.NewGuid():N}";

        await Clients.Group(groupName).SendAsync("BotThinking", new
        {
            roomId,
            isThinking = true,
            triggeredByName
        });

        // Hiện ngay khung bong bóng @bot (0ms chờ) trước cả khi gọi sang AI Microservice
        await Clients.Group(groupName).SendAsync("BotStreamStart", new
        {
            tempId,
            roomId,
            createdAt = DateTime.UtcNow,
            triggeredByName
        });

        _ = ProcessBotReplySafelyAsync(roomId, triggeredByUserId, triggeredByName, questionContent, tempId);
    }

    private async Task ProcessBotReplySafelyAsync(
        int roomId,
        int triggeredByUserId,
        string triggeredByName,
        string questionContent,
        string tempId)
    {
        var roomClients = _hubContext.Clients.Group(GetRoomGroupName(roomId));
        var stopwatch = Stopwatch.StartNew();
        long? firstChunkMs = null;
        var chunkBatcher = new BotStreamChunkBatcher(
            BotStreamBatchInterval,
            chunk => roomClients.SendAsync("BotStreamChunk", new { tempId, roomId, chunk }),
            _logger);

        try
        {
            // Scope mới cho DbContext: Hub đã kết thúc nên tuyệt đối không dùng lại các Scoped Service của Hub
            using var scope = _scopeFactory.CreateScope();
            var scopedChatService = scope.ServiceProvider.GetRequiredService<IChatService>();

            var result = await scopedChatService.StreamAndSaveBotReplyAsync(
                roomId,
                triggeredByUserId,
                questionContent,
                chunk =>
                {
                    firstChunkMs ??= stopwatch.ElapsedMilliseconds;
                    chunkBatcher.Append(chunk);
                    return Task.CompletedTask;
                });

            await chunkBatcher.CompleteAsync();
            if (result.StreamFallbackReason is not null)
            {
                _logger.LogWarning(
                    "Luồng stream @bot tại phòng {RoomId} lỗi, đã dùng endpoint không stream: {Reason}",
                    roomId,
                    result.StreamFallbackReason);
            }

            await roomClients.SendAsync("BotStreamComplete", new
            {
                tempId,
                roomId,
                messageId = result.Message.Id,
                fullContent = result.Message.Content,
                durationMs = stopwatch.ElapsedMilliseconds,
                firstChunkMs = firstChunkMs ?? stopwatch.ElapsedMilliseconds,
                isStreamed = result.StreamFallbackReason is null,
                message = result.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi bất đồng bộ khi xử lý phản hồi @bot tại phòng {RoomId}.", roomId);
            await chunkBatcher.CompleteAsync();
            await roomClients.SendAsync("BotStreamFailed", new
            {
                tempId,
                roomId,
                message = "Trợ giảng AI gặp sự cố khi trả lời. Vui lòng gửi lại câu hỏi."
            });
        }
        finally
        {
            await chunkBatcher.DisposeAsync();
            await roomClients.SendAsync("BotThinking", new
            {
                roomId,
                isThinking = false,
                triggeredByName
            });
        }
    }

    private (int UserId, string FullName, string? AvatarUrl, bool IsAdmin) GetCallerIdentity()
    {
        var rawId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(rawId, out var userId))
        {
            throw new ForbiddenAccessException("Không xác định được phiên đăng nhập SignalR.");
        }

        var fullName = Context.User?.FindFirstValue(ClaimTypes.Name) ?? "Sinh viên";
        var avatarUrl = Context.User?.FindFirstValue("avatar_url");
        var role = Context.User?.FindFirstValue(ClaimTypes.Role);
        var isAdmin = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);

        return (userId, fullName, avatarUrl, isAdmin);
    }

    private async Task EnsureAuthorizedRoomSessionAsync(int roomId, int userId, bool isAdmin)
    {
        var cacheKey = $"joined:{roomId}";
        if (Context.Items.TryGetValue(cacheKey, out var flag) && flag is true)
        {
            return;
        }

        await _roomService.EnsureUserIsRoomMemberAsync(roomId, userId, isAdmin);
        Context.Items[cacheKey] = true;
    }
}
