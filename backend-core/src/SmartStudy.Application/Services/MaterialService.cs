using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SmartStudy.Application.Common.Interfaces;
using SmartStudy.Application.DTOs.Materials;
using SmartStudy.Application.Validators;
using SmartStudy.Domain.Entities;
using SmartStudy.Domain.Enums;
using SmartStudy.Domain.Exceptions;

namespace SmartStudy.Application.Services;

/// <summary>
/// Hợp đồng dịch vụ Quản lý Học liệu PDF, gọi AI bóc tách tóm tắt và đồng bộ thời gian thực.
/// </summary>
public interface IMaterialService
{
    Task<IReadOnlyList<MaterialDto>> GetRoomMaterialsAsync(int roomId, int currentUserId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<MaterialDetailDto> GetMaterialDetailAsync(int materialId, int currentUserId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<MaterialDto> UploadMaterialAsync(
        int roomId,
        int currentUserId,
        string title,
        Stream fileStream,
        string originalFileName,
        long fileSizeBytes,
        CancellationToken cancellationToken = default);
    Task<MaterialMindmapDto> GenerateMaterialMindmapAsync(
        int materialId,
        int currentUserId,
        bool isAdmin,
        bool forceRegenerate = false,
        CancellationToken cancellationToken = default);
    Task DeleteMaterialAsync(int materialId, int currentUserId, bool isAdmin, CancellationToken cancellationToken = default);
}

/// <summary>
/// Triển khai quy trình tải lên PDF, lưu trữ vật lý, gọi FastAPI tóm tắt và phát tín hiệu SignalR.
/// </summary>
public class MaterialService : IMaterialService
{
    private const long MaxPdfSizeBytes = 25 * 1024 * 1024;

    private readonly IAppDbContext _context;
    private readonly IRoomService _roomService;
    private readonly IFileStorageService _fileStorage;
    private readonly IAiMicroserviceClient _aiClient;
    private readonly IStudyRoomRealtimeNotifier _realtimeNotifier;
    private readonly IValidator<UploadMaterialMetadataDto> _metadataValidator;

    public MaterialService(
        IAppDbContext context,
        IRoomService roomService,
        IFileStorageService fileStorage,
        IAiMicroserviceClient aiClient,
        IStudyRoomRealtimeNotifier realtimeNotifier,
        IValidator<UploadMaterialMetadataDto> metadataValidator)
    {
        _context = context;
        _roomService = roomService;
        _fileStorage = fileStorage;
        _aiClient = aiClient;
        _realtimeNotifier = realtimeNotifier;
        _metadataValidator = metadataValidator;
    }

    public async Task<IReadOnlyList<MaterialDto>> GetRoomMaterialsAsync(
        int roomId,
        int currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        await _roomService.EnsureUserIsRoomMemberAsync(roomId, currentUserId, isAdmin, cancellationToken);

        return await _context.CourseMaterials
            .AsNoTracking()
            .Where(m => m.RoomId == roomId && (isAdmin || m.IsApproved))
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new MaterialDto
            {
                Id = m.Id,
                RoomId = m.RoomId,
                UploadedById = m.UploadedById,
                UploadedByName = m.UploadedBy.FullName,
                Title = m.Title,
                OriginalFileName = m.OriginalFileName,
                FileUrl = m.StoredFilePath,
                FileSizeBytes = m.FileSizeBytes,
                PageCount = m.PageCount,
                AiSummary = m.AiSummary,
                AiMindmap = m.AiMindmap,
                HasExtractedText = !string.IsNullOrEmpty(m.ExtractedText),
                IsApproved = m.IsApproved,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<MaterialDetailDto> GetMaterialDetailAsync(
        int materialId,
        int currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var material = await _context.CourseMaterials
            .AsNoTracking()
            .Include(m => m.UploadedBy)
            .FirstOrDefaultAsync(m => m.Id == materialId, cancellationToken)
            ?? throw new NotFoundException("Học liệu PDF", materialId);

        await _roomService.EnsureUserIsRoomMemberAsync(material.RoomId, currentUserId, isAdmin, cancellationToken);

        return new MaterialDetailDto
        {
            Id = material.Id,
            RoomId = material.RoomId,
            UploadedById = material.UploadedById,
            UploadedByName = material.UploadedBy.FullName,
            Title = material.Title,
            OriginalFileName = material.OriginalFileName,
            FileUrl = material.StoredFilePath,
            FileSizeBytes = material.FileSizeBytes,
            PageCount = material.PageCount,
            AiSummary = material.AiSummary,
            AiMindmap = material.AiMindmap,
            HasExtractedText = !string.IsNullOrEmpty(material.ExtractedText),
            ExtractedText = material.ExtractedText,
            IsApproved = material.IsApproved,
            CreatedAt = material.CreatedAt
        };
    }

    public async Task<MaterialDto> UploadMaterialAsync(
        int roomId,
        int currentUserId,
        string title,
        Stream fileStream,
        string originalFileName,
        long fileSizeBytes,
        CancellationToken cancellationToken = default)
    {
        await _roomService.EnsureUserIsRoomMemberAsync(roomId, currentUserId, isAdmin: false, cancellationToken);

        var effectiveTitle = string.IsNullOrWhiteSpace(title)
            ? Path.GetFileNameWithoutExtension(originalFileName)
            : title.Trim();

        await ValidationHelper.ValidateOrThrowAsync(
            _metadataValidator,
            new UploadMaterialMetadataDto { Title = effectiveTitle },
            cancellationToken);

        if (fileSizeBytes <= 0 || fileSizeBytes > MaxPdfSizeBytes)
        {
            throw new DomainException("Tệp PDF phải có dung lượng lớn hơn 0 và không vượt quá 25 MB.");
        }

        if (!Path.GetExtension(originalFileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("Hệ thống chỉ chấp nhận học liệu định dạng .pdf.");
        }

        var uploader = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == currentUserId, cancellationToken)
            ?? throw new NotFoundException("Người dùng", currentUserId);

        using var memoryBuffer = new MemoryStream();
        await fileStream.CopyToAsync(memoryBuffer, cancellationToken);
        memoryBuffer.Position = 0;

        var storedPath = await _fileStorage.SaveMaterialPdfAsync(memoryBuffer, originalFileName, cancellationToken);

        memoryBuffer.Position = 0;
        var aiExtraction = await _aiClient.ExtractPdfAsync(
            memoryBuffer,
            originalFileName,
            effectiveTitle,
            cancellationToken);

        _context.AiUsageLogs.Add(new AiUsageLog
        {
            UserId = currentUserId,
            RoomId = roomId,
            FeatureType = AiFeatureType.ExtractPdf,
            ModelName = aiExtraction.Usage.ModelName,
            PromptTokens = aiExtraction.Usage.PromptTokens,
            CompletionTokens = aiExtraction.Usage.CompletionTokens,
            TotalTokens = aiExtraction.Usage.TotalTokens,
            DurationMs = aiExtraction.Usage.DurationMs,
            IsSuccess = aiExtraction.Usage.IsSuccess,
            CreatedAt = DateTime.UtcNow
        });

        var entity = new CourseMaterial
        {
            RoomId = roomId,
            UploadedById = currentUserId,
            Title = effectiveTitle,
            OriginalFileName = originalFileName,
            StoredFilePath = storedPath,
            FileSizeBytes = fileSizeBytes,
            PageCount = Math.Max(1, aiExtraction.PageCount),
            ExtractedText = string.IsNullOrWhiteSpace(aiExtraction.ExtractedText) ? null : aiExtraction.ExtractedText,
            AiSummary = aiExtraction.AiSummary,
            IsApproved = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.CourseMaterials.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        await _realtimeNotifier.NotifyWorkspaceResourceUpdatedAsync(
            roomId,
            "Material",
            entity.Title,
            uploader.FullName,
            cancellationToken);

        return new MaterialDto
        {
            Id = entity.Id,
            RoomId = entity.RoomId,
            UploadedById = entity.UploadedById,
            UploadedByName = uploader.FullName,
            Title = entity.Title,
            OriginalFileName = entity.OriginalFileName,
            FileUrl = entity.StoredFilePath,
            FileSizeBytes = entity.FileSizeBytes,
            PageCount = entity.PageCount,
            AiSummary = entity.AiSummary,
            AiMindmap = entity.AiMindmap,
            HasExtractedText = !string.IsNullOrEmpty(entity.ExtractedText),
            IsApproved = entity.IsApproved,
            CreatedAt = entity.CreatedAt
        };
    }

    public async Task<MaterialMindmapDto> GenerateMaterialMindmapAsync(
        int materialId,
        int currentUserId,
        bool isAdmin,
        bool forceRegenerate = false,
        CancellationToken cancellationToken = default)
    {
        var material = await _context.CourseMaterials
            .FirstOrDefaultAsync(m => m.Id == materialId, cancellationToken)
            ?? throw new NotFoundException("Học liệu PDF", materialId);

        await _roomService.EnsureUserIsRoomMemberAsync(material.RoomId, currentUserId, isAdmin, cancellationToken);

        if (!forceRegenerate && !string.IsNullOrWhiteSpace(material.AiMindmap))
        {
            return new MaterialMindmapDto
            {
                MaterialId = material.Id,
                Title = $"Sơ đồ Tư duy: {material.Title}",
                MermaidCode = material.AiMindmap,
                NodeCount = Math.Max(4, material.AiMindmap.Count(ch => ch == '['))
            };
        }

        var sourceText = !string.IsNullOrWhiteSpace(material.ExtractedText)
            ? material.ExtractedText
            : (material.AiSummary ?? material.Title);

        var aiResult = await _aiClient.GenerateMindmapAsync(
            material.Title,
            sourceText,
            material.AiSummary,
            cancellationToken);

        material.AiMindmap = aiResult.MermaidCode;

        _context.AiUsageLogs.Add(new AiUsageLog
        {
            UserId = currentUserId,
            RoomId = material.RoomId,
            FeatureType = AiFeatureType.GenerateMindmap,
            ModelName = aiResult.Usage.ModelName,
            PromptTokens = aiResult.Usage.PromptTokens,
            CompletionTokens = aiResult.Usage.CompletionTokens,
            TotalTokens = aiResult.Usage.TotalTokens,
            DurationMs = aiResult.Usage.DurationMs,
            IsSuccess = aiResult.Usage.IsSuccess,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        return new MaterialMindmapDto
        {
            MaterialId = material.Id,
            Title = aiResult.Title,
            MermaidCode = aiResult.MermaidCode,
            NodeCount = aiResult.NodeCount
        };
    }

    public async Task DeleteMaterialAsync(int materialId, int currentUserId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var material = await _context.CourseMaterials
            .Include(m => m.Room)
            .FirstOrDefaultAsync(m => m.Id == materialId, cancellationToken)
            ?? throw new NotFoundException("Học liệu PDF", materialId);

        var isRoomOwner = material.Room.CreatedById == currentUserId;
        if (!isAdmin && !isRoomOwner && material.UploadedById != currentUserId)
        {
            throw new ForbiddenAccessException("Chỉ người tải lên, Trưởng phòng hoặc Quản trị viên mới được xóa tài liệu này.");
        }

        // Xóa an toàn các Quiz, QuizAttemptAnswer và FlashcardDeck phụ thuộc vào tài liệu này (FK NoAction)
        var relatedQuizIds = await _context.Quizzes
            .Where(q => q.MaterialId == materialId)
            .Select(q => q.Id)
            .ToListAsync(cancellationToken);

        if (relatedQuizIds.Count > 0)
        {
            var relatedAnswers = await _context.QuizAttemptAnswers
                .Where(a => relatedQuizIds.Contains(a.Question.QuizId))
                .ToListAsync(cancellationToken);
            _context.QuizAttemptAnswers.RemoveRange(relatedAnswers);

            var relatedQuizzes = await _context.Quizzes
                .Where(q => q.MaterialId == materialId)
                .ToListAsync(cancellationToken);
            _context.Quizzes.RemoveRange(relatedQuizzes);
        }

        var relatedDecks = await _context.FlashcardDecks
            .Where(d => d.MaterialId == materialId)
            .ToListAsync(cancellationToken);
        _context.FlashcardDecks.RemoveRange(relatedDecks);

        var filePath = material.StoredFilePath;
        _context.CourseMaterials.Remove(material);
        await _context.SaveChangesAsync(cancellationToken);

        await _fileStorage.DeleteFileIfExistsAsync(filePath, cancellationToken);
    }
}
