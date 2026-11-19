using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.Application.Common.Interfaces;
using SmartStudy.Application.Common.Models;
using SmartStudy.Application.DTOs.Quizzes;
using SmartStudy.Application.Services;

namespace SmartStudy.API.Controllers;

/// <summary>
/// Thin Controller điều phối Sinh đề Trắc nghiệm AI, Làm bài, Chấm điểm và Xem lại lịch sử thi.
/// </summary>
[ApiController]
[Authorize]
public class QuizzesController : ControllerBase
{
    private readonly IQuizService _quizService;
    private readonly ICurrentUserService _currentUser;

    public QuizzesController(IQuizService quizService, ICurrentUserService currentUser)
    {
        _quizService = quizService;
        _currentUser = currentUser;
    }

    [HttpGet("api/rooms/{roomId:int}/quizzes")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<QuizSummaryDto>>>> GetRoomQuizzes(
        int roomId,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var data = await _quizService.GetRoomQuizzesAsync(roomId, userId, _currentUser.IsAdmin, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<QuizSummaryDto>>.Ok(data));
    }

    [HttpPost("api/rooms/{roomId:int}/quizzes/generate")]
    public async Task<ActionResult<ApiResponse<QuizSummaryDto>>> GenerateQuiz(
        int roomId,
        [FromBody] GenerateQuizRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var data = await _quizService.GenerateQuizFromMaterialAsync(roomId, userId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<QuizSummaryDto>.Ok(data, "Gemini AI đã biên soạn đề thi trắc nghiệm thành công."));
    }

    [HttpGet("api/quizzes/{quizId:int}")]
    public async Task<ActionResult<ApiResponse<QuizTakeDto>>> GetQuizForTaking(
        int quizId,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var data = await _quizService.GetQuizForTakingAsync(quizId, userId, _currentUser.IsAdmin, cancellationToken);
        return Ok(ApiResponse<QuizTakeDto>.Ok(data));
    }

    [HttpPost("api/quizzes/{quizId:int}/submit")]
    public async Task<ActionResult<ApiResponse<QuizAttemptResultDto>>> SubmitQuiz(
        int quizId,
        [FromBody] SubmitQuizRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var data = await _quizService.SubmitQuizAttemptAsync(quizId, userId, request, cancellationToken);
        return Ok(ApiResponse<QuizAttemptResultDto>.Ok(data, $"Hoàn thành bài thi! Điểm số của bạn: {data.Score:0.00}/10."));
    }

    [HttpGet("api/quizzes/my-attempts")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<QuizAttemptHistoryItemDto>>>> GetMyAttempts(
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var data = await _quizService.GetMyAttemptHistoryAsync(userId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<QuizAttemptHistoryItemDto>>.Ok(data));
    }

    [HttpGet("api/quizzes/attempts/{attemptId:int}")]
    public async Task<ActionResult<ApiResponse<QuizAttemptResultDto>>> GetAttemptDetail(
        int attemptId,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var data = await _quizService.GetAttemptDetailAsync(attemptId, userId, _currentUser.IsAdmin, cancellationToken);
        return Ok(ApiResponse<QuizAttemptResultDto>.Ok(data));
    }

    [HttpPost("api/quizzes/analyze-weakness")]
    public async Task<ActionResult<ApiResponse<WeaknessAnalysisResultDto>>> AnalyzeWeakness(
        [FromBody] AnalyzeWeaknessRequestDto? request,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var data = await _quizService.AnalyzeMyWeaknessesAsync(userId, request, cancellationToken);
        return Ok(ApiResponse<WeaknessAnalysisResultDto>.Ok(data, "AI đã hoàn tất chẩn đoán lỗ hổng kiến thức."));
    }

    [HttpPost("api/quizzes/generate-remedial")]
    public async Task<ActionResult<ApiResponse<QuizSummaryDto>>> GenerateRemedialQuiz(
        [FromBody] GenerateRemedialQuizRequestDto? request,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var data = await _quizService.GenerateRemedialQuizAsync(userId, request ?? new GenerateRemedialQuizRequestDto(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<QuizSummaryDto>.Ok(data, "Đã biên soạn ngay đề Quiz 5 câu vá lỗ hổng kiến thức!"));
    }

    private int RequireUserId()
    {
        return _currentUser.UserId ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ.");
    }
}
