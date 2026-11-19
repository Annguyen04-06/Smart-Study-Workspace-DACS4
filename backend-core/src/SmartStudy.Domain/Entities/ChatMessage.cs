using SmartStudy.Domain.Common;

namespace SmartStudy.Domain.Entities;

/// <summary>
/// Thực thể Tin nhắn thảo luận trong phòng học (sử dụng khóa chính BIGINT cho tần suất ghi cao).
/// Khi IsAiBot = true, SenderId sẽ nhận giá trị null.
/// </summary>
public class ChatMessage : BaseEntity<long>
{
    public int RoomId { get; set; }
    public int? SenderId { get; set; }
    public bool IsAiBot { get; set; }
    public bool IsPinned { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ReactionsJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public StudyRoom Room { get; set; } = null!;
    public User? Sender { get; set; }
}
