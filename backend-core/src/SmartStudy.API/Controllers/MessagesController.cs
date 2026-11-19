using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.Application.Common.Interfaces;
using SmartStudy.Application.Common.Models;
using SmartStudy.Application.DTOs.Chat;
using SmartStudy.Application.Services;

namespace SmartStudy.API.Controllers;

/// <summary>
/// Thin Controller phục vụ tải phân trang lịch sử tin nhắn và quản lý danh sách Tin nhắn Đã ghim (Pinned Messages).
/// </summary>
[ApiController]
[Route("api/rooms/{roomId:int}/messages")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ICurrentUserService _currentUser;

    public MessagesController(IChatService chatService, ICurrentUserService currentUser)
    {
        _chatService = chatService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Phân trang cuộn ngược (beforeId) hoặc Delta Sync lấy tin mới hơn (afterId) khi điện thoại mở lại App.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ChatMessageDto>>>> GetRoomMessages(
        int roomId,
        [FromQuery] long? beforeId = null,
        [FromQuery] long? afterId = null,
        [FromQuery] int pageSize = 35,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var data = afterId is > 0
            ? await _chatService.GetRoomMessagesAfterAsync(roomId, userId, _currentUser.IsAdmin, afterId.Value, pageSize, cancellationToken)
            : await _chatService.GetRoomMessagesAsync(roomId, userId, _currentUser.IsAdmin, beforeId, pageSize, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ChatMessageDto>>.Ok(data));
    }

    [HttpGet("pinned")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ChatMessageDto>>>> GetPinnedMessages(
        int roomId,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var data = await _chatService.GetPinnedMessagesAsync(roomId, userId, _currentUser.IsAdmin, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ChatMessageDto>>.Ok(data));
    }

    [HttpPatch("{messageId:long}/pin")]
    public async Task<ActionResult<ApiResponse<ChatMessageDto>>> TogglePinMessage(
        int roomId,
        long messageId,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var data = await _chatService.TogglePinMessageAsync(roomId, messageId, userId, _currentUser.IsAdmin, cancellationToken);
        var msg = data.IsPinned ? "Đã ghim kiến thức vào đầu phòng học." : "Đã bỏ ghim tin nhắn.";
        return Ok(ApiResponse<ChatMessageDto>.Ok(data, msg));
    }

    [HttpPost("{messageId:long}/reactions")]
    public async Task<ActionResult<ApiResponse<ChatMessageDto>>> ToggleMessageReaction(
        int roomId,
        long messageId,
        [FromBody] ToggleReactionRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var data = await _chatService.ToggleMessageReactionAsync(
            roomId,
            messageId,
            userId,
            request.Emoji,
            _currentUser.IsAdmin,
            cancellationToken);
        return Ok(ApiResponse<ChatMessageDto>.Ok(data));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ChatMessageDto>>> PostMessageFallback(
        int roomId,
        [FromBody] SendMessageRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var data = await _chatService.SaveStudentMessageAsync(roomId, userId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<ChatMessageDto>.Ok(data));
    }
}
