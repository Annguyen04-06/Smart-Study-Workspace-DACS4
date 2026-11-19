using System.Security.Claims;
using SmartStudy.Application.Common.Interfaces;

namespace SmartStudy.API.Services;

/// <summary>
/// Trích xuất thông tin định danh người dùng hiện hành từ JWT Claims trong HttpContext.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public int? UserId
    {
        get
        {
            var rawId = Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? Principal?.FindFirstValue("sub");
            return int.TryParse(rawId, out var parsed) ? parsed : null;
        }
    }

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);
    public string? FullName => Principal?.FindFirstValue(ClaimTypes.Name);
    public string? Role => Principal?.FindFirstValue(ClaimTypes.Role);
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true && UserId.HasValue;
    public bool IsAdmin => string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase);
}
