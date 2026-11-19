using SmartStudy.Domain.Common;
using SmartStudy.Domain.Enums;

namespace SmartStudy.Domain.Entities;

/// <summary>
/// Thực thể Nhật ký tiêu thụ Token Gemini AI phục vụ biểu đồ phân tích Quản trị hệ thống.
/// </summary>
public class AiUsageLog : BaseEntity
{
    public int UserId { get; set; }
    public int? RoomId { get; set; }
    public AiFeatureType FeatureType { get; set; }
    public string ModelName { get; set; } = "gemini-2.5-flash";
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public int DurationMs { get; set; }
    public bool IsSuccess { get; set; } = true;

    public User User { get; set; } = null!;
    public StudyRoom? Room { get; set; }
}
