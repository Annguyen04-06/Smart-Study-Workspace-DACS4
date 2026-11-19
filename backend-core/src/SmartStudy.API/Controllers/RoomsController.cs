using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.API.Hubs;
using SmartStudy.Application.Common.Interfaces;
using SmartStudy.Application.Common.Models;
using SmartStudy.Application.DTOs.Chat;
using SmartStudy.Application.DTOs.Rooms;
using SmartStudy.Application.Services;

namespace SmartStudy.API.Controllers;

/// <summary>
/// Thin Controller điều phối các thao tác Phòng học nhóm và tham gia bằng mã mời InviteCode.
/// </summary>
[ApiController]
[Route("api/rooms")]
[Authorize]
public class RoomsController : ControllerBase
{
    private readonly IRoomService _roomService;
    private readonly ICurrentUserService _currentUser;
    private readonly PresenceTracker _presenceTracker;

    public RoomsController(
        IRoomService roomService,
        ICurrentUserService currentUser,
        PresenceTracker presenceTracker)
    {
        _roomService = roomService;
        _currentUser = currentUser;
        _presenceTracker = presenceTracker;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<RoomDto>>>> GetRooms(
        [FromQuery] RoomFilterQueryDto filter,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var data = await _roomService.GetRoomsAsync(userId, filter, cancellationToken);
        return Ok(ApiResponse<PagedResult<RoomDto>>.Ok(data));
    }

    [HttpGet("my")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RoomDto>>>> GetMyRooms(CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var data = await _roomService.GetMyJoinedRoomsAsync(userId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<RoomDto>>.Ok(data));
    }

    [HttpGet("{roomId:int}")]
    public async Task<ActionResult<ApiResponse<RoomDetailDto>>> GetRoomDetail(
        int roomId,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var data = await _roomService.GetRoomDetailAsync(roomId, userId, _currentUser.IsAdmin, cancellationToken);
        return Ok(ApiResponse<RoomDetailDto>.Ok(data));
    }

    [HttpGet("{roomId:int}/online-users")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<OnlineRoomUserDto>>>> GetOnlineUsers(
        int roomId,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        await _roomService.EnsureUserIsRoomMemberAsync(roomId, userId, _currentUser.IsAdmin, cancellationToken);
        var online = _presenceTracker.GetOnlineUsers(roomId);
        return Ok(ApiResponse<IReadOnlyList<OnlineRoomUserDto>>.Ok(online));
    }

    [HttpGet("{roomId:int}/leaderboard")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RoomLeaderboardItemDto>>>> GetLeaderboard(
        int roomId,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var leaderboard = await _roomService.GetRoomLeaderboardAsync(roomId, userId, _currentUser.IsAdmin, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<RoomLeaderboardItemDto>>.Ok(leaderboard));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<RoomDto>>> CreateRoom(
        [FromBody] CreateRoomRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var data = await _roomService.CreateRoomAsync(userId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<RoomDto>.Ok(data, "Khởi tạo phòng học nhóm thành công."));
    }

    [HttpPost("join")]
    public async Task<ActionResult<ApiResponse<RoomDto>>> JoinByInviteCode(
        [FromBody] JoinRoomByCodeRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var data = await _roomService.JoinRoomByInviteCodeAsync(userId, request, cancellationToken);
        return Ok(ApiResponse<RoomDto>.Ok(data, $"Đã tham gia phòng học '{data.Name}'."));
    }

    [HttpPost("{roomId:int}/join")]
    public async Task<ActionResult<ApiResponse<RoomDto>>> JoinByRoomId(
        int roomId,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var data = await _roomService.JoinRoomByIdAsync(roomId, userId, cancellationToken);
        return Ok(ApiResponse<RoomDto>.Ok(data, $"Đã tham gia phòng học '{data.Name}'."));
    }

    [HttpDelete("{roomId:int}/leave")]
    public async Task<ActionResult<ApiResponse>> LeaveRoom(
        int roomId,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        await _roomService.LeaveRoomAsync(roomId, userId, cancellationToken);
        return Ok(ApiResponse.OkMessage("Đã rời khỏi phòng học."));
    }

    [HttpDelete("{roomId:int}")]
    public async Task<ActionResult<ApiResponse>> DeleteRoom(
        int roomId,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        await _roomService.DeleteRoomAsync(roomId, userId, _currentUser.IsAdmin, cancellationToken);
        return Ok(ApiResponse.OkMessage("Đã xóa phòng học thành công."));
    }

    private int RequireUserId()
    {
        return _currentUser.UserId ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ.");
    }
}
