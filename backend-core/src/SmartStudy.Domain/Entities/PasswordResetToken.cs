using SmartStudy.Domain.Common;

namespace SmartStudy.Domain.Entities;

/// <summary>
/// Thực thể lưu mã OTP khôi phục mật khẩu (6 chữ số đã băm bảo mật, thời hạn 10 phút, chống vét cạn).
/// </summary>
public class PasswordResetToken : BaseEntity
{
    public int UserId { get; set; }
    public string OtpHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public int AttemptCount { get; set; }

    public User User { get; set; } = null!;
}
