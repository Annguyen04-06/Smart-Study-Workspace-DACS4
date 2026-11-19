using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.Application.Common.Interfaces;
using SmartStudy.Application.Common.Models;
using SmartStudy.Application.DTOs.Admin;
using SmartStudy.Application.Services;

namespace SmartStudy.API.Controllers;

/// <summary>
/// Thin Controller dành riêng cho Quản trị viên (Role = Admin) giám sát hệ thống và Token Gemini AI.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IRoomService _roomService;
    private readonly IMaterialService _materialService;
    private readonly ICurrentUserService _currentUser;

    public AdminController(
        IAdminService adminService,
        IRoomService roomService,
        IMaterialService materialService,
        ICurrentUserService currentUser)
    {
        _adminService = adminService;
        _roomService = roomService;
        _materialService = materialService;
        _currentUser = currentUser;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<ApiResponse<AdminOverviewDto>>> GetOverview(CancellationToken cancellationToken)
    {
        var data = await _adminService.GetOverviewAnalyticsAsync(cancellationToken);
        return Ok(ApiResponse<AdminOverviewDto>.Ok(data));
    }

    [HttpGet("users")]
    public async Task<ActionResult<ApiResponse<PagedResult<AdminUserDto>>>> GetUsers(
        [FromQuery] string? search = null,
        [FromQuery] string? role = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 15,
        CancellationToken cancellationToken = default)
    {
        var data = await _adminService.GetUsersAsync(search, role, pageNumber, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<AdminUserDto>>.Ok(data));
    }

    [HttpPatch("users/{userId:int}/toggle-lock")]
    public async Task<ActionResult<ApiResponse<AdminUserDto>>> ToggleUserLock(
        int userId,
        CancellationToken cancellationToken)
    {
        var adminId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var data = await _adminService.ToggleUserLockAsync(userId, adminId, cancellationToken);
        var msg = data.IsLocked ? $"Đã khóa tài khoản '{data.FullName}'." : $"Đã mở khóa tài khoản '{data.FullName}'.";
        return Ok(ApiResponse<AdminUserDto>.Ok(data, msg));
    }

    [HttpGet("rooms")]
    public async Task<ActionResult<ApiResponse<PagedResult<AdminRoomDto>>>> GetRooms(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 15,
        CancellationToken cancellationToken = default)
    {
        var data = await _adminService.GetRoomsAsync(search, status, pageNumber, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<AdminRoomDto>>.Ok(data));
    }

    [HttpPatch("rooms/{roomId:int}/status")]
    public async Task<ActionResult<ApiResponse<AdminRoomDto>>> UpdateRoomStatus(
        int roomId,
        [FromBody] UpdateRoomStatusRequestDto request,
        CancellationToken cancellationToken)
    {
        var data = await _adminService.UpdateRoomStatusAsync(roomId, request, cancellationToken);
        return Ok(ApiResponse<AdminRoomDto>.Ok(data, $"Đã cập nhật trạng thái phòng '{data.Name}' sang {data.Status}."));
    }

    [HttpDelete("rooms/{roomId:int}")]
    public async Task<ActionResult<ApiResponse>> DeleteRoom(int roomId, CancellationToken cancellationToken)
    {
        var adminId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        await _roomService.DeleteRoomAsync(roomId, adminId, isAdmin: true, cancellationToken);
        return Ok(ApiResponse.OkMessage("Đã xóa vĩnh viễn phòng học."));
    }

    [HttpGet("materials")]
    public async Task<ActionResult<ApiResponse<PagedResult<AdminMaterialDto>>>> GetMaterials(
        [FromQuery] string? search = null,
        [FromQuery] bool? approvedOnly = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 15,
        CancellationToken cancellationToken = default)
    {
        var data = await _adminService.GetMaterialsAsync(search, approvedOnly, pageNumber, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<AdminMaterialDto>>.Ok(data));
    }

    [HttpPatch("materials/{materialId:int}/toggle-approval")]
    public async Task<ActionResult<ApiResponse<AdminMaterialDto>>> ToggleMaterialApproval(
        int materialId,
        CancellationToken cancellationToken)
    {
        var data = await _adminService.ToggleMaterialApprovalAsync(materialId, cancellationToken);
        var msg = data.IsApproved ? "Đã phê duyệt hiển thị học liệu PDF." : "Đã tạm ẩn học liệu PDF.";
        return Ok(ApiResponse<AdminMaterialDto>.Ok(data, msg));
    }

    [HttpDelete("materials/{materialId:int}")]
    public async Task<ActionResult<ApiResponse>> DeleteMaterial(int materialId, CancellationToken cancellationToken)
    {
        var adminId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        await _materialService.DeleteMaterialAsync(materialId, adminId, isAdmin: true, cancellationToken);
        return Ok(ApiResponse.OkMessage("Đã xóa học liệu PDF khỏi hệ thống."));
    }

    [HttpGet("ai-logs")]
    public async Task<ActionResult<ApiResponse<PagedResult<AdminAiUsageLogDto>>>> GetAiLogs(
        [FromQuery] string? featureType = null,
        [FromQuery] bool? isSuccess = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var data = await _adminService.GetAiUsageLogsAsync(featureType, isSuccess, pageNumber, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<AdminAiUsageLogDto>>.Ok(data));
    }
}
