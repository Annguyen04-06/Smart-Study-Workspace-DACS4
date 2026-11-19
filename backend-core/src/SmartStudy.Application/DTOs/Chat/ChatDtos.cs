namespace SmartStudy.Application.DTOs.Chat;

/// <summary>
/// Các DTO truyền tải Tin nhắn thời gian thực của Sinh viên, Trợ giảng AI (@bot) và trạng thái Ghim (IsPinned).
/// </summary>
public class SendMessageRequestDto
{
    public string Content { get; set; } = string.Empty;
}

public class ToggleReactionRequestDto
{
    public string Emoji { get; set; } = string.Empty;
}

public class ChatMessageDto
{
    public long Id { get; set; }
    public int RoomId { get; set; }
    public int? SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string? SenderAvatarUrl { get; set; }
    public bool IsAiBot { get; set; }
    public bool IsPinned { get; set; }
    public string Content { get; set; } = string.Empty;
    public Dictionary<string, List<int>> Reactions { get; set; } = new();
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Mã tạm do trình duyệt sinh khi gửi (Optimistic UI) — gửi kèm ngược lại qua SignalR để khớp bong bóng "đang gửi"
    /// với tin nhắn thật. Không lưu vào Database.
    /// </summary>
    public string? ClientMessageId { get; set; }
}

/// <summary>
/// Kết quả xử lý @bot: tin nhắn đã lưu và lý do phải chuyển sang endpoint không stream (null nếu stream thành công).
/// </summary>
public sealed record BotReplyResult(ChatMessageDto Message, string? StreamFallbackReason);

public class OnlineRoomUserDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}
