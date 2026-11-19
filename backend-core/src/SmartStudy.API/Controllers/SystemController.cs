using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.API.Services;
using SmartStudy.Application.Common.Models;

namespace SmartStudy.API.Controllers;

/// <summary>
/// Thin Controller cung cấp thông tin phiên bản hệ thống phục vụ OTA Auto-Sync
/// giúp PWA trên Laptop và Điện thoại tự động nhận diện bản build mới nhất trên máy chủ.
/// </summary>
[ApiController]
[Route("api/system")]
public class SystemController : ControllerBase
{
    private readonly AppVersionProvider _versionProvider;
    private readonly MobileAccessProvider _mobileAccessProvider;

    public SystemController(AppVersionProvider versionProvider, MobileAccessProvider mobileAccessProvider)
    {
        _versionProvider = versionProvider;
        _mobileAccessProvider = mobileAccessProvider;
    }

    [HttpGet("version")]
    [AllowAnonymous]
    public ActionResult<ApiResponse<object>> GetVersion()
    {
        ApplyNoStoreHeaders();
        var info = _versionProvider.GetCurrentVersion();
        return Ok(ApiResponse<object>.Ok(new
        {
            version = info.Version,
            updatedAt = info.UpdatedAt,
            releaseNotes = info.ReleaseNotes
        }, "Đã lấy thông tin phiên bản hệ thống mới nhất."));
    }

    /// <summary>
    /// Link mở App trên điện thoại cho nút "Mở trên Điện thoại (QR HTTPS)":
    /// { publicHttpsUrl, lanUrl, isTunnelActive, updatedAt }. Link LAN chỉ trả cho thiết bị trong mạng nội bộ.
    /// </summary>
    [HttpGet("mobile-access")]
    [AllowAnonymous]
    public ActionResult<ApiResponse<MobileAccessInfo>> GetMobileAccess()
    {
        ApplyNoStoreHeaders();
        var includeLanUrl = MobileAccessProvider.IsLocalNetworkAddress(HttpContext.Connection.RemoteIpAddress);
        var info = _mobileAccessProvider.GetMobileAccess(includeLanUrl);
        var message = info.IsTunnelActive
            ? "Đường hầm HTTPS đang hoạt động: quét QR để cài App 1 chạm trên điện thoại."
            : "Chưa có đường hầm HTTPS hoạt động; tạm dùng link mạng LAN (HTTP).";
        return Ok(ApiResponse<MobileAccessInfo>.Ok(info, message));
    }

    // Bắt buộc chống cache ở mọi lớp trình duyệt, Service Worker và CDN/Proxy
    private void ApplyNoStoreHeaders()
    {
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";
    }
}
