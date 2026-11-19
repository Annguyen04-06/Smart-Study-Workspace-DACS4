using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SmartStudy.Application.Common.Interfaces;
using SmartStudy.Application.DTOs.Flashcards;
using SmartStudy.Application.Validators;
using SmartStudy.Domain.Entities;
using SmartStudy.Domain.Enums;
using SmartStudy.Domain.Exceptions;

namespace SmartStudy.Application.Services;

/// <summary>
/// Hợp đồng dịch vụ Quản lý Bộ thẻ Ghi nhớ 3D và cập nhật mức độ thuộc bài Spaced Repetition.
/// </summary>
public interface IFlashcardService
{
    Task<IReadOnlyList<FlashcardDeckSummaryDto>> GetRoomDecksAsync(int roomId, int currentUserId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<FlashcardDeckDetailDto> GetDeckDetailAsync(int deckId, int currentUserId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<FlashcardDeckDetailDto> GenerateDeckFromMaterialAsync(int roomId, int currentUserId, GenerateFlashcardDeckRequestDto request, CancellationToken cancellationToken = default);
    Task<FlashcardItemDto> UpdateCardMasteryAsync(int cardId, int currentUserId, UpdateFlashcardMasteryRequestDto request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Triển khai sinh bộ thẻ Flashcard từ PDF bằng Gemini AI và theo dõi Spaced Repetition (0: Chưa thuộc, 1: Tạm nhớ, 2: Đã thuộc).
/// </summary>
public class FlashcardService : IFlashcardService
{
    private readonly IAppDbContext _context;
    private readonly IRoomService _roomService;
    private readonly IAiMicroserviceClient _aiClient;
    private readonly IStudyRoomRealtimeNotifier _realtimeNotifier;
    private readonly IValidator<GenerateFlashcardDeckRequestDto> _generateValidator;
    private readonly IValidator<UpdateFlashcardMasteryRequestDto> _masteryValidator;

    public FlashcardService(
        IAppDbContext context,
        IRoomService roomService,
        IAiMicroserviceClient aiClient,
        IStudyRoomRealtimeNotifier realtimeNotifier,
        IValidator<GenerateFlashcardDeckRequestDto> generateValidator,
        IValidator<UpdateFlashcardMasteryRequestDto> masteryValidator)
    {
        _context = context;
        _roomService = roomService;
        _aiClient = aiClient;
        _realtimeNotifier = realtimeNotifier;
        _generateValidator = generateValidator;
        _masteryValidator = masteryValidator;
    }

    public async Task<IReadOnlyList<FlashcardDeckSummaryDto>> GetRoomDecksAsync(
        int roomId,
        int currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        await _roomService.EnsureUserIsRoomMemberAsync(roomId, currentUserId, isAdmin, cancellationToken);

        return await _context.FlashcardDecks
            .AsNoTracking()
            .Where(d => d.RoomId == roomId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new FlashcardDeckSummaryDto
            {
                Id = d.Id,
                RoomId = d.RoomId,
                MaterialId = d.MaterialId,
                MaterialTitle = d.Material.Title,
                CreatedById = d.CreatedById,
                CreatedByName = d.CreatedBy.FullName,
                Title = d.Title,
                CardCount = d.CardCount,
                MasteredCount = d.Cards.Count(c => c.MasteryLevel == 2),
                LearningCount = d.Cards.Count(c => c.MasteryLevel == 1),
                UnlearnedCount = d.Cards.Count(c => c.MasteryLevel == 0),
                CreatedAt = d.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<FlashcardDeckDetailDto> GetDeckDetailAsync(
        int deckId,
        int currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var deck = await _context.FlashcardDecks
            .AsNoTracking()
            .Include(d => d.Material)
            .Include(d => d.CreatedBy)
            .Include(d => d.Cards)
            .FirstOrDefaultAsync(d => d.Id == deckId, cancellationToken)
            ?? throw new NotFoundException("Bộ thẻ ghi nhớ", deckId);

        await _roomService.EnsureUserIsRoomMemberAsync(deck.RoomId, currentUserId, isAdmin, cancellationToken);

        var cards = deck.Cards
            .OrderBy(c => c.Id)
            .Select(c => new FlashcardItemDto
            {
                Id = c.Id,
                DeckId = c.DeckId,
                FrontContent = c.FrontContent,
                BackContent = c.BackContent,
                MasteryLevel = c.MasteryLevel
            })
            .ToList();

        return new FlashcardDeckDetailDto
        {
            Id = deck.Id,
            RoomId = deck.RoomId,
            MaterialId = deck.MaterialId,
            MaterialTitle = deck.Material.Title,
            CreatedById = deck.CreatedById,
            CreatedByName = deck.CreatedBy.FullName,
            Title = deck.Title,
            CardCount = cards.Count,
            MasteredCount = cards.Count(c => c.MasteryLevel == 2),
            LearningCount = cards.Count(c => c.MasteryLevel == 1),
            UnlearnedCount = cards.Count(c => c.MasteryLevel == 0),
            CreatedAt = deck.CreatedAt,
            Cards = cards
        };
    }

    public async Task<FlashcardDeckDetailDto> GenerateDeckFromMaterialAsync(
        int roomId,
        int currentUserId,
        GenerateFlashcardDeckRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await ValidationHelper.ValidateOrThrowAsync(_generateValidator, request, cancellationToken);
        await _roomService.EnsureUserIsRoomMemberAsync(roomId, currentUserId, isAdmin: false, cancellationToken);

        var material = await _context.CourseMaterials
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.MaterialId && m.RoomId == roomId, cancellationToken)
            ?? throw new NotFoundException("Học liệu PDF trong phòng", request.MaterialId);

        if (!material.IsApproved)
        {
            throw new DomainException("Tài liệu PDF này đang bị tạm khóa kiểm duyệt.");
        }

        if (string.IsNullOrWhiteSpace(material.ExtractedText))
        {
            throw new DomainException("Tài liệu PDF chưa có nội dung trích xuất để AI tạo thẻ ghi nhớ.");
        }

        var creator = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == currentUserId, cancellationToken)
            ?? throw new NotFoundException("Người dùng", currentUserId);

        AiGeneratedFlashcardDeckResponse aiResponse;
        try
        {
            aiResponse = await _aiClient.GenerateFlashcardsAsync(
                material.Title,
                material.ExtractedText,
                material.AiSummary,
                request.CardCount,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _context.AiUsageLogs.Add(new AiUsageLog
            {
                UserId = currentUserId,
                RoomId = roomId,
                FeatureType = AiFeatureType.GenerateFlashcards,
                ModelName = "gemini-2.5-flash",
                IsSuccess = false,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync(cancellationToken);
            throw new DomainException($"Không thể sinh bộ thẻ Flashcard từ AI: {ex.Message}");
        }

        _context.AiUsageLogs.Add(new AiUsageLog
        {
            UserId = currentUserId,
            RoomId = roomId,
            FeatureType = AiFeatureType.GenerateFlashcards,
            ModelName = aiResponse.Usage.ModelName,
            PromptTokens = aiResponse.Usage.PromptTokens,
            CompletionTokens = aiResponse.Usage.CompletionTokens,
            TotalTokens = aiResponse.Usage.TotalTokens,
            DurationMs = aiResponse.Usage.DurationMs,
            IsSuccess = aiResponse.Usage.IsSuccess,
            CreatedAt = DateTime.UtcNow
        });

        var finalTitle = !string.IsNullOrWhiteSpace(request.Title)
            ? request.Title.Trim()
            : (!string.IsNullOrWhiteSpace(aiResponse.SuggestedTitle)
                ? aiResponse.SuggestedTitle.Trim()
                : $"Thẻ ghi nhớ: {material.Title}");

        var deck = new FlashcardDeck
        {
            RoomId = roomId,
            MaterialId = material.Id,
            CreatedById = currentUserId,
            Title = finalTitle.Length > 200 ? finalTitle[..200] : finalTitle,
            CardCount = aiResponse.Cards.Count,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var card in aiResponse.Cards)
        {
            var front = card.FrontContent.Trim();
            var back = card.BackContent.Trim();
            deck.Cards.Add(new Flashcard
            {
                FrontContent = front.Length > 1000 ? front[..1000] : front,
                BackContent = back.Length > 2000 ? back[..2000] : back,
                MasteryLevel = 0
            });
        }

        _context.FlashcardDecks.Add(deck);
        await _context.SaveChangesAsync(cancellationToken);

        await _realtimeNotifier.NotifyWorkspaceResourceUpdatedAsync(
            roomId,
            "FlashcardDeck",
            deck.Title,
            creator.FullName,
            cancellationToken);

        return await GetDeckDetailAsync(deck.Id, currentUserId, isAdmin: false, cancellationToken);
    }

    public async Task<FlashcardItemDto> UpdateCardMasteryAsync(
        int cardId,
        int currentUserId,
        UpdateFlashcardMasteryRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await ValidationHelper.ValidateOrThrowAsync(_masteryValidator, request, cancellationToken);

        var card = await _context.Flashcards
            .Include(c => c.Deck)
            .FirstOrDefaultAsync(c => c.Id == cardId, cancellationToken)
            ?? throw new NotFoundException("Thẻ Flashcard", cardId);

        await _roomService.EnsureUserIsRoomMemberAsync(card.Deck.RoomId, currentUserId, isAdmin: false, cancellationToken);

        // Thuật toán Spaced Repetition 3 mức: 0 = Chưa thuộc, 1 = Tạm nhớ, 2 = Đã thuộc
        card.MasteryLevel = request.MasteryLevel;
        await _context.SaveChangesAsync(cancellationToken);

        return new FlashcardItemDto
        {
            Id = card.Id,
            DeckId = card.DeckId,
            FrontContent = card.FrontContent,
            BackContent = card.BackContent,
            MasteryLevel = card.MasteryLevel
        };
    }
}
