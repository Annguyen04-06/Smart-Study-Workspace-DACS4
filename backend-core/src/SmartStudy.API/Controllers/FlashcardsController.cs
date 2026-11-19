using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.Application.Common.Interfaces;
using SmartStudy.Application.Common.Models;
using SmartStudy.Application.DTOs.Flashcards;
using SmartStudy.Application.Services;

namespace SmartStudy.API.Controllers;

/// <summary>
/// Thin Controller điều phối Sinh bộ thẻ Ghi nhớ 3D và cập nhật trạng thái Spaced Repetition.
/// </summary>
[ApiController]
[Authorize]
public class FlashcardsController : ControllerBase
{
    private readonly IFlashcardService _flashcardService;
    private readonly ICurrentUserService _currentUser;

    public FlashcardsController(IFlashcardService flashcardService, ICurrentUserService currentUser)
    {
        _flashcardService = flashcardService;
        _currentUser = currentUser;
    }

    [HttpGet("api/rooms/{roomId:int}/flashcards")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FlashcardDeckSummaryDto>>>> GetRoomDecks(
        int roomId,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var data = await _flashcardService.GetRoomDecksAsync(roomId, userId, _currentUser.IsAdmin, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<FlashcardDeckSummaryDto>>.Ok(data));
    }

    [HttpPost("api/rooms/{roomId:int}/flashcards/generate")]
    public async Task<ActionResult<ApiResponse<FlashcardDeckDetailDto>>> GenerateDeck(
        int roomId,
        [FromBody] GenerateFlashcardDeckRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var data = await _flashcardService.GenerateDeckFromMaterialAsync(roomId, userId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<FlashcardDeckDetailDto>.Ok(data, "Gemini AI đã tạo bộ thẻ ghi nhớ thành công."));
    }

    [HttpGet("api/flashcards/decks/{deckId:int}")]
    public async Task<ActionResult<ApiResponse<FlashcardDeckDetailDto>>> GetDeckDetail(
        int deckId,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var data = await _flashcardService.GetDeckDetailAsync(deckId, userId, _currentUser.IsAdmin, cancellationToken);
        return Ok(ApiResponse<FlashcardDeckDetailDto>.Ok(data));
    }

    [HttpPatch("api/flashcards/cards/{cardId:int}/mastery")]
    public async Task<ActionResult<ApiResponse<FlashcardItemDto>>> UpdateMastery(
        int cardId,
        [FromBody] UpdateFlashcardMasteryRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var data = await _flashcardService.UpdateCardMasteryAsync(cardId, userId, request, cancellationToken);
        return Ok(ApiResponse<FlashcardItemDto>.Ok(data, "Đã cập nhật mức độ ghi nhớ của thẻ."));
    }

    private int RequireUserId()
    {
        return _currentUser.UserId ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ.");
    }
}
