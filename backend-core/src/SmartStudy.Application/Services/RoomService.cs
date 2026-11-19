using System.Security.Cryptography;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SmartStudy.Application.Common.Interfaces;
using SmartStudy.Application.Common.Models;
using SmartStudy.Application.DTOs.Rooms;
using SmartStudy.Application.Validators;
using SmartStudy.Domain.Entities;
using SmartStudy.Domain.Enums;
using SmartStudy.Domain.Exceptions;

namespace SmartStudy.Application.Services;

/// <summary>
/// Hợp đồng dịch vụ Quản lý Phòng học, sinh mã mời InviteCode và phân quyền thành viên.
/// </summary>
public interface IRoomService
{
    Task<PagedResult<RoomDto>> GetRoomsAsync(int currentUserId, RoomFilterQueryDto filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoomDto>> GetMyJoinedRoomsAsync(int currentUserId, CancellationToken cancellationToken = default);
    Task<RoomDetailDto> GetRoomDetailAsync(int roomId, int currentUserId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoomLeaderboardItemDto>> GetRoomLeaderboardAsync(int roomId, int currentUserId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<RoomDto> CreateRoomAsync(int currentUserId, CreateRoomRequestDto request, CancellationToken cancellationToken = default);
    Task<RoomDto> JoinRoomByInviteCodeAsync(int currentUserId, JoinRoomByCodeRequestDto request, CancellationToken cancellationToken = default);
    Task<RoomDto> JoinRoomByIdAsync(int roomId, int currentUserId, CancellationToken cancellationToken = default);
    Task LeaveRoomAsync(int roomId, int currentUserId, CancellationToken cancellationToken = default);
    Task DeleteRoomAsync(int roomId, int currentUserId, bool isAdmin, CancellationToken cancellationToken = default);
    Task EnsureUserIsRoomMemberAsync(int roomId, int userId, bool isAdmin, CancellationToken cancellationToken = default);
    string GenerateInviteCode(int length = 8);
}

/// <summary>
/// Triển khai toàn bộ nghiệp vụ Phòng học nhóm: tìm kiếm, tạo phòng, tham gia bằng mã mời và rời phòng.
/// </summary>
public class RoomService : IRoomService
{
    private const string InviteCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly IAppDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly IValidator<CreateRoomRequestDto> _createValidator;
    private readonly IValidator<JoinRoomByCodeRequestDto> _joinValidator;

    public RoomService(
        IAppDbContext context,
        IFileStorageService fileStorage,
        IValidator<CreateRoomRequestDto> createValidator,
        IValidator<JoinRoomByCodeRequestDto> joinValidator)
    {
        _context = context;
        _fileStorage = fileStorage;
        _createValidator = createValidator;
        _joinValidator = joinValidator;
    }

    public string GenerateInviteCode(int length = 8)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = InviteCodeAlphabet[RandomNumberGenerator.GetInt32(InviteCodeAlphabet.Length)];
        }
        return new string(chars);
    }

    public async Task<PagedResult<RoomDto>> GetRoomsAsync(
        int currentUserId,
        RoomFilterQueryDto filter,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Clamp(filter.PageSize, 1, 50);

        var query = _context.StudyRooms
            .AsNoTracking()
            .Where(r => r.Status == RoomStatus.Active || r.Members.Any(m => m.UserId == currentUserId));

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var keyword = filter.Search.Trim().ToLower();
            query = query.Where(r =>
                r.Name.ToLower().Contains(keyword) ||
                r.SubjectCategory.ToLower().Contains(keyword) ||
                r.InviteCode.ToLower().Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(filter.SubjectCategory))
        {
            var subject = filter.SubjectCategory.Trim().ToLower();
            query = query.Where(r => r.SubjectCategory.ToLower() == subject);
        }

        if (filter.OnlyJoined)
        {
            query = query.Where(r => r.Members.Any(m => m.UserId == currentUserId));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.Members.Any(m => m.UserId == currentUserId))
            .ThenByDescending(r => r.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RoomDto
            {
                Id = r.Id,
                Name = r.Name,
                SubjectCategory = r.SubjectCategory,
                Description = r.Description,
                InviteCode = r.InviteCode,
                Status = r.Status.ToString(),
                CreatedById = r.CreatedById,
                CreatedByName = r.CreatedBy.FullName,
                MemberCount = r.Members.Count,
                MaterialCount = r.Materials.Count(m => m.IsApproved),
                QuizCount = r.Quizzes.Count,
                FlashcardDeckCount = r.FlashcardDecks.Count,
                IsJoined = r.Members.Any(m => m.UserId == currentUserId),
                MyRoleInRoom = r.Members
                    .Where(m => m.UserId == currentUserId)
                    .Select(m => m.RoleInRoom.ToString())
                    .FirstOrDefault(),
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return PagedResult<RoomDto>.Create(items, totalCount, pageNumber, pageSize);
    }

    public async Task<IReadOnlyList<RoomDto>> GetMyJoinedRoomsAsync(int currentUserId, CancellationToken cancellationToken = default)
    {
        return await _context.StudyRooms
            .AsNoTracking()
            .Where(r => r.Status != RoomStatus.Closed && r.Members.Any(m => m.UserId == currentUserId))
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new RoomDto
            {
                Id = r.Id,
                Name = r.Name,
                SubjectCategory = r.SubjectCategory,
                Description = r.Description,
                InviteCode = r.InviteCode,
                Status = r.Status.ToString(),
                CreatedById = r.CreatedById,
                CreatedByName = r.CreatedBy.FullName,
                MemberCount = r.Members.Count,
                MaterialCount = r.Materials.Count(m => m.IsApproved),
                QuizCount = r.Quizzes.Count,
                FlashcardDeckCount = r.FlashcardDecks.Count,
                IsJoined = true,
                MyRoleInRoom = r.Members
                    .Where(m => m.UserId == currentUserId)
                    .Select(m => m.RoleInRoom.ToString())
                    .FirstOrDefault(),
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<RoomDetailDto> GetRoomDetailAsync(
        int roomId,
        int currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        await EnsureUserIsRoomMemberAsync(roomId, currentUserId, isAdmin, cancellationToken);

        var room = await _context.StudyRooms
            .AsNoTracking()
            .Include(r => r.CreatedBy)
            .Include(r => r.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(r => r.Id == roomId, cancellationToken)
            ?? throw new NotFoundException("Phòng học", roomId);

        var materialCount = await _context.CourseMaterials.CountAsync(m => m.RoomId == roomId && m.IsApproved, cancellationToken);
        var quizCount = await _context.Quizzes.CountAsync(q => q.RoomId == roomId, cancellationToken);
        var deckCount = await _context.FlashcardDecks.CountAsync(d => d.RoomId == roomId, cancellationToken);
        var myMember = room.Members.FirstOrDefault(m => m.UserId == currentUserId);

        var members = room.Members
            .OrderByDescending(m => m.RoleInRoom == RoomMemberRole.Owner)
            .ThenBy(m => m.JoinedAt)
            .Select(m => new RoomMemberDto
            {
                UserId = m.UserId,
                StudentCode = m.User.StudentCode,
                FullName = m.User.FullName,
                Email = m.User.Email,
                AvatarUrl = m.User.AvatarUrl,
                RoleInRoom = m.RoleInRoom.ToString(),
                JoinedAt = m.JoinedAt
            })
            .ToList();

        return new RoomDetailDto
        {
            Id = room.Id,
            Name = room.Name,
            SubjectCategory = room.SubjectCategory,
            Description = room.Description,
            InviteCode = room.InviteCode,
            Status = room.Status.ToString(),
            CreatedById = room.CreatedById,
            CreatedByName = room.CreatedBy.FullName,
            MemberCount = members.Count,
            MaterialCount = materialCount,
            QuizCount = quizCount,
            FlashcardDeckCount = deckCount,
            IsJoined = myMember is not null,
            MyRoleInRoom = myMember?.RoleInRoom.ToString(),
            CreatedAt = room.CreatedAt,
            Members = members
        };
    }

    /// <summary>
    /// Thuật toán tính điểm Bảng xếp hạng Thi đua Học tập trong phòng:
    /// TotalPoints = (Điểm TB Quiz * 10) + (Số đề hoàn thành * 15) + (Tối đa 50 tin nhắn trao đổi * 2).
    /// </summary>
    public async Task<IReadOnlyList<RoomLeaderboardItemDto>> GetRoomLeaderboardAsync(
        int roomId,
        int currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        await EnsureUserIsRoomMemberAsync(roomId, currentUserId, isAdmin, cancellationToken);

        var members = await _context.RoomMembers
            .AsNoTracking()
            .Where(rm => rm.RoomId == roomId)
            .Select(rm => new
            {
                rm.UserId,
                rm.User.StudentCode,
                rm.User.FullName,
                rm.User.AvatarUrl,
                RoleInRoom = rm.RoleInRoom.ToString()
            })
            .ToListAsync(cancellationToken);

        var roomQuizAttempts = await _context.QuizAttempts
            .AsNoTracking()
            .Where(qa => qa.Quiz.RoomId == roomId)
            .Select(qa => new { qa.UserId, qa.QuizId, qa.Score })
            .ToListAsync(cancellationToken);

        var messageCounts = await _context.ChatMessages
            .AsNoTracking()
            .Where(cm => cm.RoomId == roomId && !cm.IsAiBot && cm.SenderId != null)
            .GroupBy(cm => cm.SenderId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, cancellationToken);

        var computed = members.Select(m =>
        {
            var userAttempts = roomQuizAttempts.Where(a => a.UserId == m.UserId).ToList();
            var completedQuizzes = userAttempts.Select(a => a.QuizId).Distinct().Count();
            var avgScore = userAttempts.Count > 0 ? Math.Round(userAttempts.Average(a => a.Score), 2) : 0m;
            var bestScore = userAttempts.Count > 0 ? Math.Round(userAttempts.Max(a => a.Score), 2) : 0m;
            var msgCount = messageCounts.GetValueOrDefault(m.UserId, 0);

            var totalPoints = Math.Round(
                (avgScore * 10m) + (completedQuizzes * 15m) + (Math.Min(msgCount, 50) * 2m),
                1);

            return new RoomLeaderboardItemDto
            {
                UserId = m.UserId,
                StudentCode = m.StudentCode,
                FullName = m.FullName,
                AvatarUrl = m.AvatarUrl,
                RoleInRoom = m.RoleInRoom,
                CompletedQuizzesCount = completedQuizzes,
                AverageQuizScore = avgScore,
                BestQuizScore = bestScore,
                MessageCount = msgCount,
                TotalPoints = totalPoints
            };
        })
        .OrderByDescending(x => x.TotalPoints)
        .ThenByDescending(x => x.AverageQuizScore)
        .ThenByDescending(x => x.CompletedQuizzesCount)
        .ThenBy(x => x.FullName)
        .ToList();

        for (var i = 0; i < computed.Count; i++)
        {
            computed[i].Rank = i + 1;
        }

        return computed;
    }

    public async Task<RoomDto> CreateRoomAsync(
        int currentUserId,
        CreateRoomRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await ValidationHelper.ValidateOrThrowAsync(_createValidator, request, cancellationToken);

        var creator = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == currentUserId, cancellationToken)
            ?? throw new NotFoundException("Người dùng", currentUserId);

        string inviteCode;
        do
        {
            inviteCode = GenerateInviteCode();
        }
        while (await _context.StudyRooms.AnyAsync(r => r.InviteCode == inviteCode, cancellationToken));

        var room = new StudyRoom
        {
            Name = request.Name.Trim(),
            SubjectCategory = request.SubjectCategory.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            InviteCode = inviteCode,
            Status = RoomStatus.Active,
            CreatedById = currentUserId,
            CreatedAt = DateTime.UtcNow
        };

        room.Members.Add(new RoomMember
        {
            UserId = currentUserId,
            RoleInRoom = RoomMemberRole.Owner,
            JoinedAt = DateTime.UtcNow
        });

        _context.StudyRooms.Add(room);
        await _context.SaveChangesAsync(cancellationToken);

        return new RoomDto
        {
            Id = room.Id,
            Name = room.Name,
            SubjectCategory = room.SubjectCategory,
            Description = room.Description,
            InviteCode = room.InviteCode,
            Status = room.Status.ToString(),
            CreatedById = currentUserId,
            CreatedByName = creator.FullName,
            MemberCount = 1,
            MaterialCount = 0,
            QuizCount = 0,
            FlashcardDeckCount = 0,
            IsJoined = true,
            MyRoleInRoom = RoomMemberRole.Owner.ToString(),
            CreatedAt = room.CreatedAt
        };
    }

    public async Task<RoomDto> JoinRoomByInviteCodeAsync(
        int currentUserId,
        JoinRoomByCodeRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await ValidationHelper.ValidateOrThrowAsync(_joinValidator, request, cancellationToken);

        // Chuẩn hóa mã mời: tự động cắt ký tự '#' đầu chuỗi và mọi khoảng trắng thừa (VD: "#OS209 " -> "OS209")
        var normalizedCode = new string((request.InviteCode ?? string.Empty)
            .Trim()
            .TrimStart('#')
            .Where(c => !char.IsWhiteSpace(c))
            .ToArray())
            .ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            throw new DomainException("Vui lòng nhập mã mời phòng học hợp lệ.");
        }

        var room = await _context.StudyRooms
            .Include(r => r.CreatedBy)
            .Include(r => r.Members)
            .FirstOrDefaultAsync(r => r.InviteCode.ToUpper() == normalizedCode, cancellationToken);

        if (room is null)
        {
            throw new NotFoundException($"Không tìm thấy phòng học nào khớp với mã mời '{normalizedCode}'.");
        }

        return await AddMemberIfNeededAndMapAsync(room, currentUserId, allowPendingViaInviteCode: true, cancellationToken);
    }

    public async Task<RoomDto> JoinRoomByIdAsync(int roomId, int currentUserId, CancellationToken cancellationToken = default)
    {
        var room = await _context.StudyRooms
            .Include(r => r.CreatedBy)
            .Include(r => r.Members)
            .FirstOrDefaultAsync(r => r.Id == roomId, cancellationToken)
            ?? throw new NotFoundException("Phòng học", roomId);

        return await AddMemberIfNeededAndMapAsync(room, currentUserId, allowPendingViaInviteCode: false, cancellationToken);
    }

    public async Task LeaveRoomAsync(int roomId, int currentUserId, CancellationToken cancellationToken = default)
    {
        var membership = await _context.RoomMembers
            .FirstOrDefaultAsync(rm => rm.RoomId == roomId && rm.UserId == currentUserId, cancellationToken)
            ?? throw new NotFoundException("Bạn chưa phải là thành viên của phòng học này.");

        if (membership.RoleInRoom == RoomMemberRole.Owner)
        {
            throw new DomainException("Trưởng phòng (Owner) không thể rời phòng. Bạn có thể đóng hoặc xóa phòng học.");
        }

        _context.RoomMembers.Remove(membership);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteRoomAsync(int roomId, int currentUserId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var room = await _context.StudyRooms
            .Include(r => r.Materials)
            .FirstOrDefaultAsync(r => r.Id == roomId, cancellationToken)
            ?? throw new NotFoundException("Phòng học", roomId);

        if (!isAdmin && room.CreatedById != currentUserId)
        {
            throw new ForbiddenAccessException("Chỉ Trưởng phòng hoặc Quản trị viên mới có quyền xóa phòng học.");
        }

        // Xóa trước các QuizAttemptAnswers, Quizzes và FlashcardDecks tham chiếu NoAction tới CourseMaterials
        var quizIds = await _context.Quizzes.Where(q => q.RoomId == roomId).Select(q => q.Id).ToListAsync(cancellationToken);
        if (quizIds.Count > 0)
        {
            var attemptAnswers = await _context.QuizAttemptAnswers
                .Where(a => quizIds.Contains(a.Question.QuizId))
                .ToListAsync(cancellationToken);
            _context.QuizAttemptAnswers.RemoveRange(attemptAnswers);

            var quizzes = await _context.Quizzes.Where(q => q.RoomId == roomId).ToListAsync(cancellationToken);
            _context.Quizzes.RemoveRange(quizzes);
        }

        var decks = await _context.FlashcardDecks.Where(d => d.RoomId == roomId).ToListAsync(cancellationToken);
        _context.FlashcardDecks.RemoveRange(decks);

        var filePaths = room.Materials.Select(m => m.StoredFilePath).ToList();
        _context.StudyRooms.Remove(room);
        await _context.SaveChangesAsync(cancellationToken);

        foreach (var path in filePaths)
        {
            await _fileStorage.DeleteFileIfExistsAsync(path, cancellationToken);
        }
    }

    public async Task EnsureUserIsRoomMemberAsync(int roomId, int userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var room = await _context.StudyRooms.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roomId, cancellationToken)
            ?? throw new NotFoundException("Phòng học", roomId);

        if (isAdmin)
        {
            return;
        }

        if (room.Status == RoomStatus.Closed)
        {
            throw new ForbiddenAccessException("Phòng học này đã bị đóng bởi Quản trị viên.");
        }

        var isMember = await _context.RoomMembers.AnyAsync(
            rm => rm.RoomId == roomId && rm.UserId == userId,
            cancellationToken);

        if (!isMember)
        {
            throw new ForbiddenAccessException("Bạn cần tham gia phòng học trước khi truy cập nội dung bên trong.");
        }
    }

    private async Task<RoomDto> AddMemberIfNeededAndMapAsync(
        StudyRoom room,
        int currentUserId,
        bool allowPendingViaInviteCode,
        CancellationToken cancellationToken)
    {
        if (room.Status == RoomStatus.Closed)
        {
            throw new DomainException("Phòng học này đã kết thúc và đóng tiếp nhận thành viên.");
        }

        var existingMember = room.Members.FirstOrDefault(m => m.UserId == currentUserId);

        if (room.Status == RoomStatus.Pending && !allowPendingViaInviteCode && existingMember is null)
        {
            throw new DomainException("Phòng học đang chờ phê duyệt mở công khai. Bạn có thể vào trực tiếp bằng Mã mời từ Trưởng phòng.");
        }

        if (existingMember is null)
        {
            existingMember = new RoomMember
            {
                RoomId = room.Id,
                UserId = currentUserId,
                RoleInRoom = RoomMemberRole.Member,
                JoinedAt = DateTime.UtcNow
            };
            _context.RoomMembers.Add(existingMember);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var materialCount = await _context.CourseMaterials.CountAsync(m => m.RoomId == room.Id && m.IsApproved, cancellationToken);
        var quizCount = await _context.Quizzes.CountAsync(q => q.RoomId == room.Id, cancellationToken);
        var deckCount = await _context.FlashcardDecks.CountAsync(d => d.RoomId == room.Id, cancellationToken);
        var memberCount = await _context.RoomMembers.CountAsync(m => m.RoomId == room.Id, cancellationToken);

        return new RoomDto
        {
            Id = room.Id,
            Name = room.Name,
            SubjectCategory = room.SubjectCategory,
            Description = room.Description,
            InviteCode = room.InviteCode,
            Status = room.Status.ToString(),
            CreatedById = room.CreatedById,
            CreatedByName = room.CreatedBy.FullName,
            MemberCount = memberCount,
            MaterialCount = materialCount,
            QuizCount = quizCount,
            FlashcardDeckCount = deckCount,
            IsJoined = true,
            MyRoleInRoom = existingMember.RoleInRoom.ToString(),
            CreatedAt = room.CreatedAt
        };
    }
}
