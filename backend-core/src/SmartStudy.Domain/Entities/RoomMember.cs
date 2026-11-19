using SmartStudy.Domain.Common;
using SmartStudy.Domain.Enums;

namespace SmartStudy.Domain.Entities;

/// <summary>
/// Thực thể kết nối Sinh viên với Phòng học kèm vai trò (Owner | Member).
/// </summary>
public class RoomMember : BaseEntity<int>
{
    public int RoomId { get; set; }
    public int UserId { get; set; }
    public RoomMemberRole RoleInRoom { get; set; } = RoomMemberRole.Member;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public StudyRoom Room { get; set; } = null!;
    public User User { get; set; } = null!;
}
