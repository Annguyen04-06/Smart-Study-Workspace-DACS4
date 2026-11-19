using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SmartStudy.Application.Common.Interfaces;
using SmartStudy.Application.Common.Models;
using SmartStudy.Application.DTOs.Admin;
using SmartStudy.Application.Validators;
using SmartStudy.Domain.Enums;
using SmartStudy.Domain.Exceptions;

namespace SmartStudy.Application.Services;

/// <summary>
/// Hợp đồng dịch vụ Quản trị hệ thống: thống kê KPI, dữ liệu biểu đồ Token 7 ngày và quản trị tài nguyên.
/// </summary>
public interface IAdminService
{
    Task<AdminOverviewDto> GetOverviewAnalyticsAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<AdminUserDto>> GetUsersAsync(string? search, string? role, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<AdminUserDto> ToggleUserLockAsync(int targetUserId, int currentAdminId, CancellationToken cancellationToken = default);
    Task<PagedResult<AdminRoomDto>> GetRoomsAsync(string? search, string? status, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<AdminRoomDto> UpdateRoomStatusAsync(int roomId, UpdateRoomStatusRequestDto request, CancellationToken cancellationToken = default);
    Task<PagedResult<AdminMaterialDto>> GetMaterialsAsync(string? search, bool? approvedOnly, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<AdminMaterialDto> ToggleMaterialApprovalAsync(int materialId, CancellationToken cancellationToken = default);
    Task<PagedResult<AdminAiUsageLogDto>> GetAiUsageLogsAsync(string? featureType, bool? isSuccess, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}

/// <summary>
/// Triển khai nghiệp vụ Cổng Quản trị viên (Admin Analytics & Moderation Portal).
/// </summary>
public class AdminService : IAdminService
{
    private readonly IAppDbContext _context;
    private readonly IValidator<UpdateRoomStatusRequestDto> _roomStatusValidator;

    public AdminService(
        IAppDbContext context,
        IValidator<UpdateRoomStatusRequestDto> roomStatusValidator)
    {
        _context = context;
        _roomStatusValidator = roomStatusValidator;
    }

    public async Task<AdminOverviewDto> GetOverviewAnalyticsAsync(CancellationToken cancellationToken = default)
    {
        var totalUsers = await _context.Users.CountAsync(cancellationToken);
        var activeStudents = await _context.Users.CountAsync(u => u.Role == UserRole.Student && !u.IsLocked, cancellationToken);
        var lockedUsers = await _context.Users.CountAsync(u => u.IsLocked, cancellationToken);

        var totalRooms = await _context.StudyRooms.CountAsync(cancellationToken);
        var activeRooms = await _context.StudyRooms.CountAsync(r => r.Status == RoomStatus.Active, cancellationToken);
        var totalMaterials = await _context.CourseMaterials.CountAsync(cancellationToken);
        var totalQuizzes = await _context.Quizzes.CountAsync(cancellationToken);
        var totalDecks = await _context.FlashcardDecks.CountAsync(cancellationToken);

        var totalAiRequests = await _context.AiUsageLogs.CountAsync(cancellationToken);
        var successfulRequests = await _context.AiUsageLogs.CountAsync(l => l.IsSuccess, cancellationToken);
        var totalTokens = await _context.AiUsageLogs.SumAsync(l => (long?)l.TotalTokens, cancellationToken) ?? 0L;

        var successRate = totalAiRequests > 0
            ? Math.Round((decimal)successfulRequests / totalAiRequests * 100m, 1)
            : 100m;

        // Chuỗi 14 ngày liên tiếp gần nhất (tự điền 0 cho ngày không phát sinh truy vấn để biểu đồ liền mạch)
        var todayUtc = DateTime.UtcNow.Date;
        var startDayUtc = todayUtc.AddDays(-13);

        var recentLogs = await _context.AiUsageLogs
            .AsNoTracking()
            .Where(l => l.CreatedAt >= startDayUtc)
            .Select(l => new
            {
                Date = l.CreatedAt.Date,
                l.PromptTokens,
                l.CompletionTokens,
                l.TotalTokens
            })
            .ToListAsync(cancellationToken);

        var dailyPoints = new List<DailyTokenUsagePointDto>(14);
        for (var offset = 0; offset < 14; offset++)
        {
            var date = startDayUtc.AddDays(offset);
            var dayItems = recentLogs.Where(x => x.Date == date).ToList();

            dailyPoints.Add(new DailyTokenUsagePointDto
            {
                DateLabel = date.ToString("dd/MM"),
                DateIso = date.ToString("yyyy-MM-dd"),
                PromptTokens = dayItems.Sum(x => x.PromptTokens),
                CompletionTokens = dayItems.Sum(x => x.CompletionTokens),
                TotalTokens = dayItems.Sum(x => x.TotalTokens),
                RequestCount = dayItems.Count
            });
        }

        // Phân bổ tỷ lệ sử dụng theo 4 nhóm tính năng AI
        var rawBreakdown = await _context.AiUsageLogs
            .AsNoTracking()
            .GroupBy(l => l.FeatureType)
            .Select(g => new
            {
                Feature = g.Key,
                Count = g.Count(),
                Tokens = g.Sum(x => x.TotalTokens)
            })
            .ToListAsync(cancellationToken);

        var allFeatures = new (AiFeatureType Type, string DisplayName)[]
        {
            (AiFeatureType.ExtractPdf, "Tóm tắt PDF"),
            (AiFeatureType.BotChat, "Trợ giảng @bot"),
            (AiFeatureType.GenerateQuiz, "Sinh đề Trắc nghiệm"),
            (AiFeatureType.GenerateFlashcards, "Sinh thẻ Flashcard"),
            (AiFeatureType.GenerateMindmap, "Sơ đồ Tư duy AI"),
            (AiFeatureType.AnalyzeWeakness, "Phân tích Lỗ hổng AI")
        };

        var featureBreakdown = allFeatures.Select(f =>
        {
            var matched = rawBreakdown.FirstOrDefault(x => x.Feature == f.Type);
            return new AiFeatureBreakdownDto
            {
                FeatureType = f.Type.ToString(),
                DisplayName = f.DisplayName,
                RequestCount = matched?.Count ?? 0,
                TotalTokens = matched?.Tokens ?? 0
            };
        }).ToList();

        return new AdminOverviewDto
        {
            TotalUsers = totalUsers,
            ActiveStudents = activeStudents,
            LockedUsers = lockedUsers,
            TotalRooms = totalRooms,
            ActiveRooms = activeRooms,
            TotalMaterials = totalMaterials,
            TotalQuizzes = totalQuizzes,
            TotalFlashcardDecks = totalDecks,
            TotalAiRequests = totalAiRequests,
            TotalTokensUsed = totalTokens,
            SuccessRatePercent = successRate,
            DailyTokenUsage = dailyPoints,
            FeatureBreakdown = featureBreakdown
        };
    }

    public async Task<PagedResult<AdminUserDto>> GetUsersAsync(
        string? search,
        string? role,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _context.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim().ToLower();
            query = query.Where(u =>
                u.FullName.ToLower().Contains(kw) ||
                u.Email.ToLower().Contains(kw) ||
                (u.StudentCode != null && u.StudentCode.ToLower().Contains(kw)));
        }

        if (!string.IsNullOrWhiteSpace(role) && Enum.TryParse<UserRole>(role, true, out var parsedRole))
        {
            query = query.Where(u => u.Role == parsedRole);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AdminUserDto
            {
                Id = u.Id,
                StudentCode = u.StudentCode,
                FullName = u.FullName,
                Email = u.Email,
                Role = u.Role.ToString(),
                IsLocked = u.IsLocked,
                JoinedRoomsCount = u.RoomMemberships.Count,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return PagedResult<AdminUserDto>.Create(items, totalCount, pageNumber, pageSize);
    }

    public async Task<AdminUserDto> ToggleUserLockAsync(
        int targetUserId,
        int currentAdminId,
        CancellationToken cancellationToken = default)
    {
        if (targetUserId == currentAdminId)
        {
            throw new DomainException("Quản trị viên không thể tự khóa tài khoản của chính mình.");
        }

        var user = await _context.Users
            .Include(u => u.RoomMemberships)
            .FirstOrDefaultAsync(u => u.Id == targetUserId, cancellationToken)
            ?? throw new NotFoundException("Người dùng", targetUserId);

        user.IsLocked = !user.IsLocked;
        await _context.SaveChangesAsync(cancellationToken);

        return new AdminUserDto
        {
            Id = user.Id,
            StudentCode = user.StudentCode,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role.ToString(),
            IsLocked = user.IsLocked,
            JoinedRoomsCount = user.RoomMemberships.Count,
            CreatedAt = user.CreatedAt
        };
    }

    public async Task<PagedResult<AdminRoomDto>> GetRoomsAsync(
        string? search,
        string? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _context.StudyRooms.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim().ToLower();
            query = query.Where(r =>
                r.Name.ToLower().Contains(kw) ||
                r.SubjectCategory.ToLower().Contains(kw) ||
                r.InviteCode.ToLower().Contains(kw));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<RoomStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(r => r.Status == parsedStatus);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new AdminRoomDto
            {
                Id = r.Id,
                Name = r.Name,
                SubjectCategory = r.SubjectCategory,
                InviteCode = r.InviteCode,
                Status = r.Status.ToString(),
                CreatedByName = r.CreatedBy.FullName,
                MemberCount = r.Members.Count,
                MaterialCount = r.Materials.Count,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return PagedResult<AdminRoomDto>.Create(items, totalCount, pageNumber, pageSize);
    }

    public async Task<AdminRoomDto> UpdateRoomStatusAsync(
        int roomId,
        UpdateRoomStatusRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await ValidationHelper.ValidateOrThrowAsync(_roomStatusValidator, request, cancellationToken);

        var room = await _context.StudyRooms
            .Include(r => r.CreatedBy)
            .Include(r => r.Members)
            .Include(r => r.Materials)
            .FirstOrDefaultAsync(r => r.Id == roomId, cancellationToken)
            ?? throw new NotFoundException("Phòng học", roomId);

        room.Status = Enum.Parse<RoomStatus>(request.Status, ignoreCase: true);
        await _context.SaveChangesAsync(cancellationToken);

        return new AdminRoomDto
        {
            Id = room.Id,
            Name = room.Name,
            SubjectCategory = room.SubjectCategory,
            InviteCode = room.InviteCode,
            Status = room.Status.ToString(),
            CreatedByName = room.CreatedBy.FullName,
            MemberCount = room.Members.Count,
            MaterialCount = room.Materials.Count,
            CreatedAt = room.CreatedAt
        };
    }

    public async Task<PagedResult<AdminMaterialDto>> GetMaterialsAsync(
        string? search,
        bool? approvedOnly,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _context.CourseMaterials.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim().ToLower();
            query = query.Where(m =>
                m.Title.ToLower().Contains(kw) ||
                m.OriginalFileName.ToLower().Contains(kw) ||
                m.Room.Name.ToLower().Contains(kw));
        }

        if (approvedOnly.HasValue)
        {
            query = query.Where(m => m.IsApproved == approvedOnly.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new AdminMaterialDto
            {
                Id = m.Id,
                RoomId = m.RoomId,
                RoomName = m.Room.Name,
                Title = m.Title,
                OriginalFileName = m.OriginalFileName,
                FileUrl = m.StoredFilePath,
                FileSizeBytes = m.FileSizeBytes,
                PageCount = m.PageCount,
                UploadedByName = m.UploadedBy.FullName,
                IsApproved = m.IsApproved,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return PagedResult<AdminMaterialDto>.Create(items, totalCount, pageNumber, pageSize);
    }

    public async Task<AdminMaterialDto> ToggleMaterialApprovalAsync(int materialId, CancellationToken cancellationToken = default)
    {
        var material = await _context.CourseMaterials
            .Include(m => m.Room)
            .Include(m => m.UploadedBy)
            .FirstOrDefaultAsync(m => m.Id == materialId, cancellationToken)
            ?? throw new NotFoundException("Học liệu PDF", materialId);

        material.IsApproved = !material.IsApproved;
        await _context.SaveChangesAsync(cancellationToken);

        return new AdminMaterialDto
        {
            Id = material.Id,
            RoomId = material.RoomId,
            RoomName = material.Room.Name,
            Title = material.Title,
            OriginalFileName = material.OriginalFileName,
            FileUrl = material.StoredFilePath,
            FileSizeBytes = material.FileSizeBytes,
            PageCount = material.PageCount,
            UploadedByName = material.UploadedBy.FullName,
            IsApproved = material.IsApproved,
            CreatedAt = material.CreatedAt
        };
    }

    public async Task<PagedResult<AdminAiUsageLogDto>> GetAiUsageLogsAsync(
        string? featureType,
        bool? isSuccess,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _context.AiUsageLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(featureType) &&
            Enum.TryParse<AiFeatureType>(featureType, true, out var parsedFeature))
        {
            query = query.Where(l => l.FeatureType == parsedFeature);
        }

        if (isSuccess.HasValue)
        {
            query = query.Where(l => l.IsSuccess == isSuccess.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new AdminAiUsageLogDto
            {
                Id = l.Id,
                UserId = l.UserId,
                UserFullName = l.User.FullName,
                RoomId = l.RoomId,
                RoomName = l.Room != null ? l.Room.Name : null,
                FeatureType = l.FeatureType.ToString(),
                ModelName = l.ModelName,
                PromptTokens = l.PromptTokens,
                CompletionTokens = l.CompletionTokens,
                TotalTokens = l.TotalTokens,
                DurationMs = l.DurationMs,
                IsSuccess = l.IsSuccess,
                CreatedAt = l.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return PagedResult<AdminAiUsageLogDto>.Create(items, totalCount, pageNumber, pageSize);
    }
}
