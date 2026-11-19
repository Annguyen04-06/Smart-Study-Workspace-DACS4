using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStudy.Application.Common.Interfaces;
using SmartStudy.Application.Common.Models;
using SmartStudy.Application.DTOs.Auth;
using SmartStudy.Application.Services;
using SmartStudy.Domain.Exceptions;

namespace SmartStudy.API.Controllers;

/// <summary>
/// Thin Controller điều phối Đăng ký, Đăng nhập JWT và Quản lý Hồ sơ cá nhân.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ICurrentUserService _currentUser;

    public AuthController(
        IAuthService authService,
        ICurrentUserService currentUser)
    {
        _authService = authService;
        _currentUser = currentUser;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Register(
        [FromBody] RegisterRequestDto request,
        CancellationToken cancellationToken)
    {
        var data = await _authService.RegisterAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<AuthResponseDto>.Ok(data, "Đăng ký tài khoản sinh viên thành công."));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Login(
        [FromBody] LoginRequestDto request,
        CancellationToken cancellationToken)
    {
        var data = await _authService.LoginAsync(request, cancellationToken);
        return Ok(ApiResponse<AuthResponseDto>.Ok(data, "Đăng nhập hệ thống thành công."));
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ForgotPasswordResponseDto>>> ForgotPassword(
        [FromBody] ForgotPasswordRequestDto request,
        CancellationToken cancellationToken)
    {
        var data = await _authService.RequestPasswordResetOtpAsync(request, cancellationToken);
        return Ok(ApiResponse<ForgotPasswordResponseDto>.Ok(
            data,
            $"Đã gửi mã xác thực 6 chữ số tới hộp thư {data.MaskedEmail}. Vui lòng kiểm tra Email của bạn."));
    }

    [HttpPost("verify-reset-otp")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse>> VerifyResetOtp(
        [FromBody] VerifyResetOtpRequestDto request,
        CancellationToken cancellationToken)
    {
        await _authService.VerifyPasswordResetOtpAsync(request, cancellationToken);
        return Ok(ApiResponse.OkMessage("Mã xác thực OTP hợp lệ."));
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse>> ResetPassword(
        [FromBody] ResetPasswordWithOtpRequestDto request,
        CancellationToken cancellationToken)
    {
        await _authService.ResetPasswordWithOtpAsync(request, cancellationToken);
        return Ok(ApiResponse.OkMessage("Đặt lại mật khẩu mới thành công! Bạn có thể đăng nhập ngay."));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> GetMe(CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var data = await _authService.GetCurrentProfileAsync(userId, cancellationToken);
        return Ok(ApiResponse<UserProfileDto>.Ok(data));
    }

    [HttpPut("profile")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> UpdateProfile(
        [FromBody] UpdateProfileRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var data = await _authService.UpdateProfileAsync(userId, request, cancellationToken);
        return Ok(ApiResponse<UserProfileDto>.Ok(data, "Cập nhật hồ sơ cá nhân thành công."));
    }

    [HttpPut("change-password")]
    [Authorize]
    public async Task<ActionResult<ApiResponse>> ChangePassword(
        [FromBody] ChangePasswordRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        await _authService.ChangePasswordAsync(userId, request, cancellationToken);
        return Ok(ApiResponse.OkMessage("Đổi mật khẩu thành công."));
    }

    [HttpPost("avatar")]
    [Authorize]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> UploadAvatar(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new DomainException("Vui lòng chọn tệp ảnh đại diện hợp lệ.");
        }

        var userId = RequireUserId();
        await using var stream = file.OpenReadStream();
        var data = await _authService.UpdateAvatarAsync(userId, stream, file.FileName, file.Length, cancellationToken);
        return Ok(ApiResponse<UserProfileDto>.Ok(data, "Cập nhật ảnh đại diện thành công."));
    }

    private int RequireUserId()
    {
        return _currentUser.UserId ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ.");
    }
}
