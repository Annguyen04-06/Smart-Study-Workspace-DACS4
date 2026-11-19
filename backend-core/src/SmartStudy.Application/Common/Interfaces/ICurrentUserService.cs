namespace SmartStudy.Application.Common.Interfaces;

/// <summary>
/// Hợp đồng đọc định danh người dùng đang đăng nhập từ HttpContext / ClaimsPrincipal.
/// </summary>
public interface ICurrentUserService
{
    int? UserId { get; }
    string? Email { get; }
    string? FullName { get; }
    string? Role { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }
}
