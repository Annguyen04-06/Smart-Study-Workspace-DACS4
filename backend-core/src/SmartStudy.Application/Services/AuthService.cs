using System.Security.Cryptography;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SmartStudy.Application.Common.Interfaces;
using SmartStudy.Application.DTOs.Auth;
using SmartStudy.Application.Validators;
using SmartStudy.Domain.Entities;
using SmartStudy.Domain.Enums;
using SmartStudy.Domain.Exceptions;

namespace SmartStudy.Application.Services;

/// <summary>
/// Hợp đồng dịch vụ Xác thực JWT, Đăng ký sinh viên, Khôi phục mật khẩu OTP và Quản lý Hồ sơ cá nhân.
/// </summary>
public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    Task<ForgotPasswordResponseDto> RequestPasswordResetOtpAsync(ForgotPasswordRequestDto request, CancellationToken cancellationToken = default);
    Task VerifyPasswordResetOtpAsync(VerifyResetOtpRequestDto request, CancellationToken cancellationToken = default);
    Task ResetPasswordWithOtpAsync(ResetPasswordWithOtpRequestDto request, CancellationToken cancellationToken = default);
    Task<UserProfileDto> GetCurrentProfileAsync(int userId, CancellationToken cancellationToken = default);
    Task<UserProfileDto> UpdateProfileAsync(int userId, UpdateProfileRequestDto request, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(int userId, ChangePasswordRequestDto request, CancellationToken cancellationToken = default);
    Task<UserProfileDto> UpdateAvatarAsync(int userId, Stream imageStream, string fileName, long fileSizeBytes, CancellationToken cancellationToken = default);
}

/// <summary>
/// Triển khai nghiệp vụ Đăng ký, Đăng nhập JWT, Khôi phục mật khẩu OTP qua Gmail SMTP và Cập nhật hồ sơ.
/// </summary>
public class AuthService : IAuthService
{
    private const long MaxAvatarSizeBytes = 5 * 1024 * 1024;
    private const int OtpExpiryMinutes = 10;
    private const int MaxOtpAttempts = 5;
    private static readonly string[] AllowedAvatarExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    private readonly IAppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IFileStorageService _fileStorage;
    private readonly IEmailSender? _emailSender;
    private readonly IValidator<RegisterRequestDto> _registerValidator;
    private readonly IValidator<LoginRequestDto> _loginValidator;
    private readonly IValidator<UpdateProfileRequestDto> _updateProfileValidator;
    private readonly IValidator<ChangePasswordRequestDto> _changePasswordValidator;

    public AuthService(
        IAppDbContext context,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IFileStorageService fileStorage,
        IValidator<RegisterRequestDto> registerValidator,
        IValidator<LoginRequestDto> loginValidator,
        IValidator<UpdateProfileRequestDto> updateProfileValidator,
        IValidator<ChangePasswordRequestDto> changePasswordValidator,
        IEmailSender? emailSender = null)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _fileStorage = fileStorage;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _updateProfileValidator = updateProfileValidator;
        _changePasswordValidator = changePasswordValidator;
        _emailSender = emailSender;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
    {
        await ValidationHelper.ValidateOrThrowAsync(_registerValidator, request, cancellationToken);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedStudentCode = request.StudentCode.Trim().ToUpperInvariant();

        var emailExists = await _context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);
        if (emailExists)
        {
            throw new DomainException("Địa chỉ Email này đã được đăng ký trong hệ thống.");
        }

        var codeExists = await _context.Users.AnyAsync(
            u => u.StudentCode != null && u.StudentCode.ToUpper() == normalizedStudentCode,
            cancellationToken);
        if (codeExists)
        {
            throw new DomainException("Mã số sinh viên này đã tồn tại trên hệ thống.");
        }

        var user = new User
        {
            StudentCode = normalizedStudentCode,
            FullName = request.FullName.Trim(),
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Role = UserRole.Student,
            IsLocked = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        return await BuildAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        await ValidationHelper.ValidateOrThrowAsync(_loginValidator, request, cancellationToken);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);

        if (user is null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new DomainException("Email hoặc mật khẩu đăng nhập không chính xác.");
        }

        if (user.IsLocked)
        {
            throw new ForbiddenAccessException("Tài khoản của bạn đã bị tạm khóa bởi Quản trị viên.");
        }

        return await BuildAuthResponseAsync(user, cancellationToken);
    }

    public async Task<ForgotPasswordResponseDto> RequestPasswordResetOtpAsync(
        ForgotPasswordRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await ValidationHelper.ValidateOrThrowAsync(new ForgotPasswordRequestValidator(), request, cancellationToken);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException($"Không tìm thấy tài khoản nào đăng ký với địa chỉ Email '{normalizedEmail}'.");
        }

        if (user.IsLocked)
        {
            throw new ForbiddenAccessException("Tài khoản này đang bị tạm khóa, vui lòng liên hệ Quản trị viên.");
        }

        // Vô hiệu hóa các mã OTP cũ chưa sử dụng của cùng người dùng
        var activeTokens = await _context.PasswordResetTokens
            .Where(t => t.UserId == user.Id && !t.IsUsed)
            .ToListAsync(cancellationToken);

        foreach (var old in activeTokens)
        {
            old.IsUsed = true;
        }

        var otpCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6");
        var resetToken = new PasswordResetToken
        {
            UserId = user.Id,
            OtpHash = _passwordHasher.HashPassword(otpCode),
            ExpiresAt = DateTime.UtcNow.AddMinutes(OtpExpiryMinutes),
            IsUsed = false,
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow
        };

        _context.PasswordResetTokens.Add(resetToken);
        await _context.SaveChangesAsync(cancellationToken);

        if (_emailSender is not null)
        {
            await _emailSender.SendPasswordResetOtpAsync(user.Email, user.FullName, otpCode, cancellationToken);
        }

        return new ForgotPasswordResponseDto
        {
            MaskedEmail = MaskEmailAddress(user.Email),
            ExpiresInSeconds = OtpExpiryMinutes * 60
        };
    }

    public async Task VerifyPasswordResetOtpAsync(
        VerifyResetOtpRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var (_, _) = await ValidateActiveResetOtpAsync(request.Email, request.OtpCode, cancellationToken);
    }

    public async Task ResetPasswordWithOtpAsync(
        ResetPasswordWithOtpRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await ValidationHelper.ValidateOrThrowAsync(new ResetPasswordWithOtpRequestValidator(), request, cancellationToken);

        var (user, token) = await ValidateActiveResetOtpAsync(request.Email, request.OtpCode, cancellationToken);

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        token.IsUsed = true;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<(User User, PasswordResetToken Token)> ValidateActiveResetOtpAsync(
        string rawEmail,
        string rawOtpCode,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = (rawEmail ?? string.Empty).Trim().ToLowerInvariant();
        var cleanOtp = new string((rawOtpCode ?? string.Empty).Where(char.IsDigit).ToArray());

        if (cleanOtp.Length != 6)
        {
            throw new DomainException("Mã xác thực OTP phải gồm đúng 6 chữ số.");
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tài khoản tương ứng với Email này.");

        var latestToken = await _context.PasswordResetTokens
            .Where(t => t.UserId == user.Id && !t.IsUsed)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestToken is null || latestToken.ExpiresAt < DateTime.UtcNow)
        {
            throw new DomainException("Mã xác thực đã hết hạn hoặc không tồn tại. Vui lòng yêu cầu gửi lại mã mới.");
        }

        if (latestToken.AttemptCount >= MaxOtpAttempts)
        {
            latestToken.IsUsed = true;
            await _context.SaveChangesAsync(cancellationToken);
            throw new DomainException("Bạn đã nhập sai mã OTP quá 5 lần. Vui lòng yêu cầu cấp mã mới.");
        }

        if (!_passwordHasher.VerifyPassword(cleanOtp, latestToken.OtpHash))
        {
            latestToken.AttemptCount++;
            await _context.SaveChangesAsync(cancellationToken);
            var remaining = Math.Max(0, MaxOtpAttempts - latestToken.AttemptCount);
            throw new DomainException($"Mã xác thực OTP không chính xác (còn {remaining} lượt thử).");
        }

        return (user, latestToken);
    }

    private static string MaskEmailAddress(string email)
    {
        var atIdx = email.IndexOf('@');
        if (atIdx <= 2)
        {
            return email;
        }
        return $"{email[..2]}***{email[(atIdx - 1)..]}";
    }

    public async Task<UserProfileDto> GetCurrentProfileAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Người dùng", userId);

        return await MapUserProfileAsync(user, cancellationToken);
    }

    public async Task<UserProfileDto> UpdateProfileAsync(int userId, UpdateProfileRequestDto request, CancellationToken cancellationToken = default)
    {
        await ValidationHelper.ValidateOrThrowAsync(_updateProfileValidator, request, cancellationToken);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Người dùng", userId);

        if (!string.IsNullOrWhiteSpace(request.StudentCode) && user.Role == UserRole.Student)
        {
            var newCode = request.StudentCode.Trim().ToUpperInvariant();
            var codeTaken = await _context.Users.AnyAsync(
                u => u.Id != userId && u.StudentCode != null && u.StudentCode.ToUpper() == newCode,
                cancellationToken);
            if (codeTaken)
            {
                throw new DomainException("Mã số sinh viên này đang thuộc về một tài khoản khác.");
            }
            user.StudentCode = newCode;
        }

        user.FullName = request.FullName.Trim();
        await _context.SaveChangesAsync(cancellationToken);

        return await MapUserProfileAsync(user, cancellationToken);
    }

    public async Task ChangePasswordAsync(int userId, ChangePasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        await ValidationHelper.ValidateOrThrowAsync(_changePasswordValidator, request, cancellationToken);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Người dùng", userId);

        if (!_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            throw new DomainException("Mật khẩu hiện tại không chính xác.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserProfileDto> UpdateAvatarAsync(
        int userId,
        Stream imageStream,
        string fileName,
        long fileSizeBytes,
        CancellationToken cancellationToken = default)
    {
        if (fileSizeBytes <= 0 || fileSizeBytes > MaxAvatarSizeBytes)
        {
            throw new DomainException("Ảnh đại diện phải có dung lượng từ 1 KB đến tối đa 5 MB.");
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedAvatarExtensions.Contains(ext))
        {
            throw new DomainException("Chỉ hỗ trợ ảnh đại diện định dạng .jpg, .jpeg, .png hoặc .webp.");
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Người dùng", userId);

        var previousAvatar = user.AvatarUrl;
        var storedPath = await _fileStorage.SaveAvatarImageAsync(imageStream, fileName, cancellationToken);
        user.AvatarUrl = storedPath;

        await _context.SaveChangesAsync(cancellationToken);
        await _fileStorage.DeleteFileIfExistsAsync(previousAvatar, cancellationToken);

        return await MapUserProfileAsync(user, cancellationToken);
    }

    private async Task<AuthResponseDto> BuildAuthResponseAsync(User user, CancellationToken cancellationToken)
    {
        var (token, expiresAt) = _jwtTokenGenerator.GenerateToken(user);
        var profile = await MapUserProfileAsync(user, cancellationToken);

        return new AuthResponseDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            User = profile
        };
    }

    private async Task<UserProfileDto> MapUserProfileAsync(User user, CancellationToken cancellationToken)
    {
        var joinedRoomsCount = await _context.RoomMembers.CountAsync(rm => rm.UserId == user.Id, cancellationToken);
        var attemptScores = await _context.QuizAttempts
            .Where(qa => qa.UserId == user.Id)
            .Select(qa => qa.Score)
            .ToListAsync(cancellationToken);

        var avgScore = attemptScores.Count > 0
            ? Math.Round(attemptScores.Average(), 2)
            : 0m;

        return new UserProfileDto
        {
            Id = user.Id,
            StudentCode = user.StudentCode,
            FullName = user.FullName,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role.ToString(),
            IsLocked = user.IsLocked,
            CreatedAt = user.CreatedAt,
            JoinedRoomsCount = joinedRoomsCount,
            CompletedQuizzesCount = attemptScores.Count,
            AverageQuizScore = avgScore
        };
    }
}
