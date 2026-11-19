namespace SmartStudy.Application.DTOs.Rooms;

/// <summary>
/// Các DTO quản lý Phòng học Nhóm, tham gia bằng mã mời InviteCode, danh sách thành viên và Bảng xếp hạng (Leaderboard).
/// </summary>
public class CreateRoomRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string SubjectCategory { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class JoinRoomByCodeRequestDto
{
    public string InviteCode { get; set; } = string.Empty;
}

public class RoomFilterQueryDto
{
    public string? Search { get; set; }
    public string? SubjectCategory { get; set; }
    public bool OnlyJoined { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 12;
}

public class RoomDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SubjectCategory { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string InviteCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int CreatedById { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    public int MaterialCount { get; set; }
    public int QuizCount { get; set; }
    public int FlashcardDeckCount { get; set; }
    public bool IsJoined { get; set; }
    public string? MyRoleInRoom { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RoomMemberDto
{
    public int UserId { get; set; }
    public string? StudentCode { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string RoleInRoom { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
}

public class RoomDetailDto : RoomDto
{
    public IReadOnlyList<RoomMemberDto> Members { get; set; } = Array.Empty<RoomMemberDto>();
}

/// <summary>
/// Hạng mục vinh danh thành viên trên Bảng xếp hạng Thi đua Học tập của Phòng học.
/// </summary>
public class RoomLeaderboardItemDto
{
    public int Rank { get; set; }
    public int UserId { get; set; }
    public string? StudentCode { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string RoleInRoom { get; set; } = string.Empty;
    public int CompletedQuizzesCount { get; set; }
    public decimal AverageQuizScore { get; set; }
    public decimal BestQuizScore { get; set; }
    public int MessageCount { get; set; }
    public decimal TotalPoints { get; set; }
}
