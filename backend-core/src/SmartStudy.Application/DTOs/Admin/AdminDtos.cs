namespace SmartStudy.Application.DTOs.Admin;

/// <summary>
/// Các DTO phục vụ Cổng Quản trị viên (4 KPI, 2 Biểu đồ Chart.js và 4 Bảng quản trị).
/// </summary>
public class DailyTokenUsagePointDto
{
    public string DateLabel { get; set; } = string.Empty;
    public string DateIso { get; set; } = string.Empty;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public int RequestCount { get; set; }
}

public class AiFeatureBreakdownDto
{
    public string FeatureType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int RequestCount { get; set; }
    public int TotalTokens { get; set; }
}

public class AdminOverviewDto
{
    public int TotalUsers { get; set; }
    public int ActiveStudents { get; set; }
    public int LockedUsers { get; set; }
    public int TotalRooms { get; set; }
    public int ActiveRooms { get; set; }
    public int TotalMaterials { get; set; }
    public int TotalQuizzes { get; set; }
    public int TotalFlashcardDecks { get; set; }
    public int TotalAiRequests { get; set; }
    public long TotalTokensUsed { get; set; }
    public decimal SuccessRatePercent { get; set; }
    public IReadOnlyList<DailyTokenUsagePointDto> DailyTokenUsage { get; set; } = Array.Empty<DailyTokenUsagePointDto>();
    public IReadOnlyList<AiFeatureBreakdownDto> FeatureBreakdown { get; set; } = Array.Empty<AiFeatureBreakdownDto>();
}

public class AdminUserDto
{
    public int Id { get; set; }
    public string? StudentCode { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
    public int JoinedRoomsCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminRoomDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SubjectCategory { get; set; } = string.Empty;
    public string InviteCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CreatedByName { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    public int MaterialCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UpdateRoomStatusRequestDto
{
    public string Status { get; set; } = "Active";
}

public class AdminMaterialDto
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int PageCount { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminAiUsageLogDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserFullName { get; set; } = string.Empty;
    public int? RoomId { get; set; }
    public string? RoomName { get; set; }
    public string FeatureType { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public int DurationMs { get; set; }
    public bool IsSuccess { get; set; }
    public DateTime CreatedAt { get; set; }
}
