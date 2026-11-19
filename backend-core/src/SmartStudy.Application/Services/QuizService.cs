using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SmartStudy.Application.Common.Interfaces;
using SmartStudy.Application.DTOs.Quizzes;
using SmartStudy.Application.Validators;
using SmartStudy.Domain.Entities;
using SmartStudy.Domain.Enums;
using SmartStudy.Domain.Exceptions;

namespace SmartStudy.Application.Services;

/// <summary>
/// Hợp đồng dịch vụ Sinh đề Trắc nghiệm AI, Thi trực tuyến và Chấm điểm tự động thang 10.
/// </summary>
public interface IQuizService
{
    Task<IReadOnlyList<QuizSummaryDto>> GetRoomQuizzesAsync(int roomId, int currentUserId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<QuizTakeDto> GetQuizForTakingAsync(int quizId, int currentUserId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<QuizSummaryDto> GenerateQuizFromMaterialAsync(int roomId, int currentUserId, GenerateQuizRequestDto request, CancellationToken cancellationToken = default);
    Task<QuizAttemptResultDto> SubmitQuizAttemptAsync(int quizId, int currentUserId, SubmitQuizRequestDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QuizAttemptHistoryItemDto>> GetMyAttemptHistoryAsync(int currentUserId, CancellationToken cancellationToken = default);
    Task<QuizAttemptResultDto> GetAttemptDetailAsync(int attemptId, int currentUserId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<WeaknessAnalysisResultDto> AnalyzeMyWeaknessesAsync(int currentUserId, AnalyzeWeaknessRequestDto? request, CancellationToken cancellationToken = default);
    Task<QuizSummaryDto> GenerateRemedialQuizAsync(int currentUserId, GenerateRemedialQuizRequestDto request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Triển khai toàn bộ nghiệp vụ Quiz AI: gọi FastAPI Structured Output, ẩn đáp án khi thi và chấm điểm chuẩn thang 10.
/// </summary>
public class QuizService : IQuizService
{
    private readonly IAppDbContext _context;
    private readonly IRoomService _roomService;
    private readonly IAiMicroserviceClient _aiClient;
    private readonly IStudyRoomRealtimeNotifier _realtimeNotifier;
    private readonly IValidator<GenerateQuizRequestDto> _generateValidator;
    private readonly IValidator<SubmitQuizRequestDto> _submitValidator;

    public QuizService(
        IAppDbContext context,
        IRoomService roomService,
        IAiMicroserviceClient aiClient,
        IStudyRoomRealtimeNotifier realtimeNotifier,
        IValidator<GenerateQuizRequestDto> generateValidator,
        IValidator<SubmitQuizRequestDto> submitValidator)
    {
        _context = context;
        _roomService = roomService;
        _aiClient = aiClient;
        _realtimeNotifier = realtimeNotifier;
        _generateValidator = generateValidator;
        _submitValidator = submitValidator;
    }

    /// <summary>
    /// Thuật toán chấm điểm chuẩn thang 10 làm tròn 2 chữ số thập phân.
    /// </summary>
    public static decimal CalculateScaleTenScore(int correctCount, int totalQuestions)
    {
        if (totalQuestions <= 0 || correctCount <= 0)
        {
            return 0m;
        }

        var boundedCorrect = Math.Min(correctCount, totalQuestions);
        return Math.Round((decimal)boundedCorrect / totalQuestions * 10m, 2, MidpointRounding.AwayFromZero);
    }

    public async Task<IReadOnlyList<QuizSummaryDto>> GetRoomQuizzesAsync(
        int roomId,
        int currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        await _roomService.EnsureUserIsRoomMemberAsync(roomId, currentUserId, isAdmin, cancellationToken);

        return await _context.Quizzes
            .AsNoTracking()
            .Where(q => q.RoomId == roomId)
            .OrderByDescending(q => q.CreatedAt)
            .Select(q => new QuizSummaryDto
            {
                Id = q.Id,
                RoomId = q.RoomId,
                MaterialId = q.MaterialId,
                MaterialTitle = q.Material.Title,
                CreatedById = q.CreatedById,
                CreatedByName = q.CreatedBy.FullName,
                Title = q.Title,
                QuestionCount = q.QuestionCount,
                DurationMinutes = q.DurationMinutes,
                Difficulty = q.Difficulty.ToString(),
                AttemptCount = q.Attempts.Count,
                MyBestScore = q.Attempts
                    .Where(a => a.UserId == currentUserId)
                    .Select(a => (decimal?)a.Score)
                    .Max(),
                CreatedAt = q.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<QuizTakeDto> GetQuizForTakingAsync(
        int quizId,
        int currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var quiz = await _context.Quizzes
            .AsNoTracking()
            .Include(q => q.Material)
            .Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.Id == quizId, cancellationToken)
            ?? throw new NotFoundException("Đề thi trắc nghiệm", quizId);

        await _roomService.EnsureUserIsRoomMemberAsync(quiz.RoomId, currentUserId, isAdmin, cancellationToken);

        // Không trả về CorrectOption và Explanation để chống gian lận trước khi nộp bài
        var orderedQuestions = quiz.Questions
            .OrderBy(q => q.OrderIndex)
            .Select(q => new QuizTakeQuestionDto
            {
                Id = q.Id,
                OrderIndex = q.OrderIndex,
                QuestionText = q.QuestionText,
                OptionA = q.OptionA,
                OptionB = q.OptionB,
                OptionC = q.OptionC,
                OptionD = q.OptionD
            })
            .ToList();

        return new QuizTakeDto
        {
            Id = quiz.Id,
            RoomId = quiz.RoomId,
            MaterialId = quiz.MaterialId,
            MaterialTitle = quiz.Material.Title,
            Title = quiz.Title,
            QuestionCount = orderedQuestions.Count,
            DurationMinutes = quiz.DurationMinutes,
            Difficulty = quiz.Difficulty.ToString(),
            Questions = orderedQuestions
        };
    }

    public async Task<QuizSummaryDto> GenerateQuizFromMaterialAsync(
        int roomId,
        int currentUserId,
        GenerateQuizRequestDto request,
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
            throw new DomainException("Tài liệu PDF này đang bị tạm khóa kiểm duyệt, không thể tạo đề thi.");
        }

        if (string.IsNullOrWhiteSpace(material.ExtractedText))
        {
            throw new DomainException("Tài liệu PDF chưa có nội dung văn bản trích xuất để AI biên soạn câu hỏi.");
        }

        var creator = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == currentUserId, cancellationToken)
            ?? throw new NotFoundException("Người dùng", currentUserId);

        var difficultyEnum = Enum.Parse<QuizDifficulty>(request.Difficulty, ignoreCase: true);

        AiGeneratedQuizResponse aiResponse;
        try
        {
            aiResponse = await _aiClient.GenerateQuizAsync(
                material.Title,
                material.ExtractedText,
                material.AiSummary,
                request.QuestionCount,
                difficultyEnum.ToString(),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _context.AiUsageLogs.Add(new AiUsageLog
            {
                UserId = currentUserId,
                RoomId = roomId,
                FeatureType = AiFeatureType.GenerateQuiz,
                ModelName = "gemini-2.5-flash",
                IsSuccess = false,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync(cancellationToken);
            throw new DomainException($"Không thể sinh đề trắc nghiệm từ AI: {ex.Message}");
        }

        _context.AiUsageLogs.Add(new AiUsageLog
        {
            UserId = currentUserId,
            RoomId = roomId,
            FeatureType = AiFeatureType.GenerateQuiz,
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
                : $"Đề trắc nghiệm: {material.Title}");

        var quiz = new Quiz
        {
            RoomId = roomId,
            MaterialId = material.Id,
            CreatedById = currentUserId,
            Title = finalTitle.Length > 200 ? finalTitle[..200] : finalTitle,
            QuestionCount = aiResponse.Questions.Count,
            DurationMinutes = request.DurationMinutes,
            Difficulty = difficultyEnum,
            CreatedAt = DateTime.UtcNow
        };

        var orderIdx = 1;
        foreach (var q in aiResponse.Questions)
        {
            var normalizedCorrect = q.CorrectOption.Trim().ToUpperInvariant();
            if (normalizedCorrect is not ("A" or "B" or "C" or "D"))
            {
                normalizedCorrect = "A";
            }

            quiz.Questions.Add(new QuizQuestion
            {
                OrderIndex = orderIdx++,
                QuestionText = q.QuestionText.Trim(),
                OptionA = q.OptionA.Trim(),
                OptionB = q.OptionB.Trim(),
                OptionC = q.OptionC.Trim(),
                OptionD = q.OptionD.Trim(),
                CorrectOption = normalizedCorrect,
                Explanation = q.Explanation.Trim()
            });
        }

        _context.Quizzes.Add(quiz);
        await _context.SaveChangesAsync(cancellationToken);

        await _realtimeNotifier.NotifyWorkspaceResourceUpdatedAsync(
            roomId,
            "Quiz",
            quiz.Title,
            creator.FullName,
            cancellationToken);

        return new QuizSummaryDto
        {
            Id = quiz.Id,
            RoomId = quiz.RoomId,
            MaterialId = quiz.MaterialId,
            MaterialTitle = material.Title,
            CreatedById = currentUserId,
            CreatedByName = creator.FullName,
            Title = quiz.Title,
            QuestionCount = quiz.QuestionCount,
            DurationMinutes = quiz.DurationMinutes,
            Difficulty = quiz.Difficulty.ToString(),
            AttemptCount = 0,
            MyBestScore = null,
            CreatedAt = quiz.CreatedAt
        };
    }

    public async Task<QuizAttemptResultDto> SubmitQuizAttemptAsync(
        int quizId,
        int currentUserId,
        SubmitQuizRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await ValidationHelper.ValidateOrThrowAsync(_submitValidator, request, cancellationToken);

        var quiz = await _context.Quizzes
            .AsNoTracking()
            .Include(q => q.Room)
            .Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.Id == quizId, cancellationToken)
            ?? throw new NotFoundException("Đề thi trắc nghiệm", quizId);

        await _roomService.EnsureUserIsRoomMemberAsync(quiz.RoomId, currentUserId, isAdmin: false, cancellationToken);

        var orderedQuestions = quiz.Questions.OrderBy(q => q.OrderIndex).ToList();
        if (orderedQuestions.Count == 0)
        {
            throw new DomainException("Đề thi hiện không có câu hỏi nào để chấm điểm.");
        }

        var answerLookup = request.Answers
            .GroupBy(a => a.QuestionId)
            .ToDictionary(g => g.Key, g => g.Last().SelectedOption?.Trim().ToUpperInvariant());

        var attempt = new QuizAttempt
        {
            QuizId = quiz.Id,
            UserId = currentUserId,
            TotalQuestions = orderedQuestions.Count,
            TimeSpentSeconds = Math.Max(0, request.TimeSpentSeconds),
            CompletedAt = DateTime.UtcNow
        };

        var correctCount = 0;
        var reviewDetails = new List<QuizQuestionReviewDto>(orderedQuestions.Count);

        foreach (var question in orderedQuestions)
        {
            answerLookup.TryGetValue(question.Id, out var selected);
            if (selected is not ("A" or "B" or "C" or "D"))
            {
                selected = null;
            }

            var isCorrect = selected is not null &&
                            string.Equals(selected, question.CorrectOption.Trim(), StringComparison.OrdinalIgnoreCase);

            if (isCorrect)
            {
                correctCount++;
            }

            attempt.Answers.Add(new QuizAttemptAnswer
            {
                QuestionId = question.Id,
                SelectedOption = selected,
                IsCorrect = isCorrect
            });

            reviewDetails.Add(new QuizQuestionReviewDto
            {
                QuestionId = question.Id,
                OrderIndex = question.OrderIndex,
                QuestionText = question.QuestionText,
                OptionA = question.OptionA,
                OptionB = question.OptionB,
                OptionC = question.OptionC,
                OptionD = question.OptionD,
                SelectedOption = selected,
                CorrectOption = question.CorrectOption.Trim().ToUpperInvariant(),
                IsCorrect = isCorrect,
                Explanation = question.Explanation
            });
        }

        attempt.CorrectAnswersCount = correctCount;
        attempt.Score = CalculateScaleTenScore(correctCount, orderedQuestions.Count);

        _context.QuizAttempts.Add(attempt);
        await _context.SaveChangesAsync(cancellationToken);

        return new QuizAttemptResultDto
        {
            AttemptId = attempt.Id,
            QuizId = quiz.Id,
            RoomId = quiz.RoomId,
            RoomName = quiz.Room.Name,
            QuizTitle = quiz.Title,
            Difficulty = quiz.Difficulty.ToString(),
            Score = attempt.Score,
            CorrectAnswersCount = attempt.CorrectAnswersCount,
            TotalQuestions = attempt.TotalQuestions,
            TimeSpentSeconds = attempt.TimeSpentSeconds,
            CompletedAt = attempt.CompletedAt,
            Details = reviewDetails
        };
    }

    public async Task<IReadOnlyList<QuizAttemptHistoryItemDto>> GetMyAttemptHistoryAsync(
        int currentUserId,
        CancellationToken cancellationToken = default)
    {
        return await _context.QuizAttempts
            .AsNoTracking()
            .Where(a => a.UserId == currentUserId)
            .OrderByDescending(a => a.CompletedAt)
            .Select(a => new QuizAttemptHistoryItemDto
            {
                AttemptId = a.Id,
                QuizId = a.QuizId,
                QuizTitle = a.Quiz.Title,
                RoomId = a.Quiz.RoomId,
                RoomName = a.Quiz.Room.Name,
                Difficulty = a.Quiz.Difficulty.ToString(),
                Score = a.Score,
                CorrectAnswersCount = a.CorrectAnswersCount,
                TotalQuestions = a.TotalQuestions,
                TimeSpentSeconds = a.TimeSpentSeconds,
                CompletedAt = a.CompletedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<QuizAttemptResultDto> GetAttemptDetailAsync(
        int attemptId,
        int currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var attempt = await _context.QuizAttempts
            .AsNoTracking()
            .Include(a => a.Quiz)
                .ThenInclude(q => q.Room)
            .Include(a => a.Answers)
                .ThenInclude(ans => ans.Question)
            .FirstOrDefaultAsync(a => a.Id == attemptId, cancellationToken)
            ?? throw new NotFoundException("Lượt làm bài thi", attemptId);

        if (!isAdmin && attempt.UserId != currentUserId)
        {
            throw new ForbiddenAccessException("Bạn chỉ được xem chi tiết lịch sử bài thi của chính mình.");
        }

        var details = attempt.Answers
            .OrderBy(ans => ans.Question.OrderIndex)
            .Select(ans => new QuizQuestionReviewDto
            {
                QuestionId = ans.QuestionId,
                OrderIndex = ans.Question.OrderIndex,
                QuestionText = ans.Question.QuestionText,
                OptionA = ans.Question.OptionA,
                OptionB = ans.Question.OptionB,
                OptionC = ans.Question.OptionC,
                OptionD = ans.Question.OptionD,
                SelectedOption = ans.SelectedOption?.Trim(),
                CorrectOption = ans.Question.CorrectOption.Trim().ToUpperInvariant(),
                IsCorrect = ans.IsCorrect,
                Explanation = ans.Question.Explanation
            })
            .ToList();

        return new QuizAttemptResultDto
        {
            AttemptId = attempt.Id,
            QuizId = attempt.QuizId,
            RoomId = attempt.Quiz.RoomId,
            RoomName = attempt.Quiz.Room.Name,
            QuizTitle = attempt.Quiz.Title,
            Difficulty = attempt.Quiz.Difficulty.ToString(),
            Score = attempt.Score,
            CorrectAnswersCount = attempt.CorrectAnswersCount,
            TotalQuestions = attempt.TotalQuestions,
            TimeSpentSeconds = attempt.TimeSpentSeconds,
            CompletedAt = attempt.CompletedAt,
            Details = details
        };
    }

    /// <summary>
    /// Tổng hợp toàn bộ lịch sử câu trả lời (đặc biệt các câu sai trong QuizAttemptAnswers),
    /// tính biểu đồ điểm mạnh/điểm yếu theo môn và gọi AI chẩn đoán 3 lỗ hổng kiến thức trọng yếu nhất.
    /// </summary>
    public async Task<WeaknessAnalysisResultDto> AnalyzeMyWeaknessesAsync(
        int currentUserId,
        AnalyzeWeaknessRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == currentUserId, cancellationToken)
            ?? throw new NotFoundException("Người dùng", currentUserId);

        var attemptsQuery = _context.QuizAttempts
            .AsNoTracking()
            .Include(a => a.Quiz)
                .ThenInclude(q => q.Room)
            .Include(a => a.Quiz)
                .ThenInclude(q => q.Material)
            .Include(a => a.Answers)
                .ThenInclude(ans => ans.Question)
            .Where(a => a.UserId == currentUserId);

        if (request?.RoomId is int filterRoomId && filterRoomId > 0)
        {
            attemptsQuery = attemptsQuery.Where(a => a.Quiz.RoomId == filterRoomId);
        }

        var attempts = await attemptsQuery
            .OrderByDescending(a => a.CompletedAt)
            .ToListAsync(cancellationToken);

        var allAnswers = attempts
            .SelectMany(a => a.Answers.Select(ans => new
            {
                Attempt = a,
                Answer = ans,
                Room = a.Quiz.Room,
                Quiz = a.Quiz,
                Question = ans.Question
            }))
            .ToList();

        // Tìm phòng học & học liệu mặc định để đề xuất tạo đề vá lỗ hổng ngay cả khi chưa làm nhiều bài
        int? suggestedRoomId = request?.RoomId;
        int? suggestedMaterialId = null;
        string? suggestedRoomName = null;

        if (allAnswers.Count == 0)
        {
            var fallbackMaterial = await _context.CourseMaterials
                .AsNoTracking()
                .Include(m => m.Room)
                .Where(m => m.IsApproved && m.Room.Members.Any(rm => rm.UserId == currentUserId))
                .Where(m => !suggestedRoomId.HasValue || m.RoomId == suggestedRoomId.Value)
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            return new WeaknessAnalysisResultDto
            {
                AnalyzedAt = DateTime.UtcNow,
                TotalAttemptsAnalyzed = 0,
                TotalQuestionsAnswered = 0,
                TotalWrongAnswers = 0,
                OverallAccuracyPercent = 0,
                OverallDiagnosis = "Bạn chưa có lượt làm bài trắc nghiệm nào để AI phân tích lỗ hổng kiến thức. Hãy làm ít nhất 1 đề Quiz trong phòng học để kích hoạt Radar chẩn đoán thông minh!",
                StrongestSubject = "Chưa có dữ liệu",
                PrioritySubject = fallbackMaterial?.Room.SubjectCategory ?? "Chưa có dữ liệu",
                SubjectRadar = Array.Empty<TopicStrengthDto>(),
                TopWeaknesses = Array.Empty<WeaknessGapDto>(),
                StudyRoadmap = new[]
                {
                    "Bước 1: Tham gia một phòng học chuyên ngành và đọc bản tóm tắt học liệu PDF.",
                    "Bước 2: Hoàn thành 1 đề trắc nghiệm AI (5-10 câu) để hệ thống ghi nhận dữ liệu năng lực.",
                    "Bước 3: Quay lại mục AI Smart Weakness Radar để nhận chẩn đoán 3 lỗ hổng kiến thức cần vá."
                },
                SuggestedRoomId = fallbackMaterial?.RoomId,
                SuggestedMaterialId = fallbackMaterial?.Id,
                SuggestedRoomName = fallbackMaterial?.Room.Name
            };
        }

        // Thống kê độ chính xác theo từng môn học / phòng học
        var subjectRadar = allAnswers
            .GroupBy(x => new { x.Room.SubjectCategory, x.Room.Id, x.Room.Name })
            .Select(g =>
            {
                var total = g.Count();
                var correct = g.Count(x => x.Answer.IsCorrect);
                var wrong = total - correct;
                var accuracy = total > 0 ? Math.Round((double)correct / total * 100.0, 1) : 0.0;
                return new TopicStrengthDto
                {
                    SubjectCategory = g.Key.SubjectCategory,
                    RoomId = g.Key.Id,
                    RoomName = g.Key.Name,
                    TotalQuestions = total,
                    CorrectCount = correct,
                    WrongCount = wrong,
                    AccuracyPercent = accuracy
                };
            })
            .OrderBy(s => s.AccuracyPercent)
            .ThenByDescending(s => s.WrongCount)
            .ToList();

        var wrongRecords = allAnswers
            .Where(x => !x.Answer.IsCorrect)
            .OrderByDescending(x => x.Attempt.CompletedAt)
            .ToList();

        // Xác định học liệu/phòng có nhiều câu làm sai nhất để gắn vào nút "Tạo ngay đề Quiz 5 câu vá lỗ hổng"
        var mostMissedMaterialGroup = wrongRecords
            .GroupBy(x => new { x.Quiz.RoomId, x.Quiz.MaterialId, RoomName = x.Room.Name })
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        if (mostMissedMaterialGroup is not null)
        {
            suggestedRoomId = mostMissedMaterialGroup.Key.RoomId;
            suggestedMaterialId = mostMissedMaterialGroup.Key.MaterialId;
            suggestedRoomName = mostMissedMaterialGroup.Key.RoomName;
        }
        else
        {
            var latestAttempt = attempts.First();
            suggestedRoomId = latestAttempt.Quiz.RoomId;
            suggestedMaterialId = latestAttempt.Quiz.MaterialId;
            suggestedRoomName = latestAttempt.Quiz.Room.Name;
        }

        var aiStatsInput = subjectRadar
            .Select(s => new AiSubjectStatInput(s.SubjectCategory, s.RoomName, s.TotalQuestions, s.CorrectCount, s.AccuracyPercent))
            .ToList();

        var aiWrongInput = wrongRecords
            .GroupBy(x => x.Question.Id)
            .Select(g => g.First())
            .Take(25)
            .Select(x => new AiWrongAnswerInput(
                SubjectCategory: x.Room.SubjectCategory,
                RoomName: x.Room.Name,
                QuizTitle: x.Quiz.Title,
                QuestionText: x.Question.QuestionText,
                SelectedOption: x.Answer.SelectedOption,
                CorrectOption: x.Question.CorrectOption,
                Explanation: x.Question.Explanation))
            .ToList();

        AiWeaknessAnalysisResponse aiResult;
        try
        {
            aiResult = await _aiClient.AnalyzeWeaknessAsync(
                user.FullName,
                aiStatsInput,
                aiWrongInput,
                cancellationToken);

            _context.AiUsageLogs.Add(new AiUsageLog
            {
                UserId = currentUserId,
                RoomId = suggestedRoomId,
                FeatureType = AiFeatureType.AnalyzeWeakness,
                ModelName = aiResult.Usage.ModelName,
                PromptTokens = aiResult.Usage.PromptTokens,
                CompletionTokens = aiResult.Usage.CompletionTokens,
                TotalTokens = aiResult.Usage.TotalTokens,
                DurationMs = aiResult.Usage.DurationMs,
                IsSuccess = aiResult.Usage.IsSuccess,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception)
        {
            // Fallback cục bộ an toàn nếu AI Microservice đang khởi động lại -> vẫn trả đủ Radar và lỗ hổng thật từ DB
            var weakest = subjectRadar.First();
            var strongest = subjectRadar.Last();
            var localGaps = wrongRecords
                .GroupBy(x => new { x.Room.SubjectCategory, x.Quiz.Title })
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select((g, idx) => new AiWeaknessGapItem(
                    TopicName: g.Key.Title,
                    SubjectCategory: g.Key.SubjectCategory,
                    MistakeCount: g.Count(),
                    Severity: idx == 0 ? "High" : (idx == 1 ? "Medium" : "Low"),
                    RootCause: $"Bạn đã làm nhầm {g.Count()} câu hỏi thuộc chuyên đề này. Hãy xem lại giải thích chi tiết và đối chiếu giáo trình gốc.",
                    StudyAction: $"Tạo ngay đề Quiz 5 câu vá lỗ hổng và ôn lại các thẻ Flashcards 3D thuộc môn {g.Key.SubjectCategory}.",
                    RecommendedPrompt: $"@bot Tổng hợp các lỗi sai thường gặp và mẹo ghi nhớ nhanh cho chủ đề {g.Key.Title}?"))
                .ToList();

            aiResult = new AiWeaknessAnalysisResponse(
                OverallDiagnosis: $"Bạn đạt tỷ lệ chính xác trung bình {Math.Round((double)allAnswers.Count(x => x.Answer.IsCorrect) / allAnswers.Count * 100.0, 1)}% qua {attempts.Count} lượt thi. Môn nổi trội nhất là {strongest.SubjectCategory}, cần ưu tiên vá lỗ hổng ở môn {weakest.SubjectCategory}.",
                StrongestSubject: strongest.SubjectCategory,
                PrioritySubject: weakest.SubjectCategory,
                WeaknessGaps: localGaps,
                StudyRoadmap: new[]
                {
                    $"Ôn tập lại các câu trả lời sai gần đây của môn {weakest.SubjectCategory}.",
                    "Sử dụng Sổ tay Markdown ghi chú lại các định nghĩa và công thức dễ nhầm.",
                    "Làm ngay đề trắc nghiệm 5 câu vá lỗ hổng để kiểm chứng tiến bộ."
                },
                Usage: new AiTokenUsageInfo("local-fallback", 0, 0, 0, 0, false));
        }

        var totalCorrect = allAnswers.Count(x => x.Answer.IsCorrect);
        var overallAccuracy = Math.Round((double)totalCorrect / allAnswers.Count * 100.0, 1);

        return new WeaknessAnalysisResultDto
        {
            AnalyzedAt = DateTime.UtcNow,
            TotalAttemptsAnalyzed = attempts.Count,
            TotalQuestionsAnswered = allAnswers.Count,
            TotalWrongAnswers = wrongRecords.Count,
            OverallAccuracyPercent = overallAccuracy,
            OverallDiagnosis = aiResult.OverallDiagnosis,
            StrongestSubject = aiResult.StrongestSubject,
            PrioritySubject = aiResult.PrioritySubject,
            SubjectRadar = subjectRadar,
            TopWeaknesses = aiResult.WeaknessGaps.Select(g => new WeaknessGapDto
            {
                TopicName = g.TopicName,
                SubjectCategory = g.SubjectCategory,
                MistakeCount = g.MistakeCount,
                Severity = g.Severity,
                RootCause = g.RootCause,
                StudyAction = g.StudyAction,
                RecommendedPrompt = g.RecommendedPrompt
            }).ToList(),
            StudyRoadmap = aiResult.StudyRoadmap,
            SuggestedRoomId = suggestedRoomId,
            SuggestedMaterialId = suggestedMaterialId,
            SuggestedRoomName = suggestedRoomName
        };
    }

    /// <summary>
    /// Tạo ngay đề Quiz 5 câu vá lỗ hổng tập trung vào các câu sinh viên từng làm sai và học liệu tương ứng.
    /// </summary>
    public async Task<QuizSummaryDto> GenerateRemedialQuizAsync(
        int currentUserId,
        GenerateRemedialQuizRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var questionCount = Math.Clamp(request.QuestionCount <= 0 ? 5 : request.QuestionCount, 3, 10);

        CourseMaterial? targetMaterial = null;
        if (request.MaterialId is int reqMatId && reqMatId > 0)
        {
            targetMaterial = await _context.CourseMaterials
                .AsNoTracking()
                .Include(m => m.Room)
                .FirstOrDefaultAsync(m => m.Id == reqMatId && m.IsApproved, cancellationToken);
        }

        if (targetMaterial is null && request.RoomId is int reqRoomId && reqRoomId > 0)
        {
            targetMaterial = await _context.CourseMaterials
                .AsNoTracking()
                .Include(m => m.Room)
                .Where(m => m.RoomId == reqRoomId && m.IsApproved && !string.IsNullOrWhiteSpace(m.ExtractedText))
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (targetMaterial is null)
        {
            // Tự động chọn học liệu có nhiều câu trả lời sai nhất của sinh viên
            var topMissedMaterialId = await _context.QuizAttemptAnswers
                .AsNoTracking()
                .Where(ans => ans.Attempt.UserId == currentUserId && !ans.IsCorrect)
                .GroupBy(ans => ans.Attempt.Quiz.MaterialId)
                .OrderByDescending(g => g.Count())
                .Select(g => (int?)g.Key)
                .FirstOrDefaultAsync(cancellationToken);

            if (topMissedMaterialId.HasValue)
            {
                targetMaterial = await _context.CourseMaterials
                    .AsNoTracking()
                    .Include(m => m.Room)
                    .FirstOrDefaultAsync(m => m.Id == topMissedMaterialId.Value && m.IsApproved, cancellationToken);
            }
        }

        if (targetMaterial is null)
        {
            targetMaterial = await _context.CourseMaterials
                .AsNoTracking()
                .Include(m => m.Room)
                .Where(m => m.IsApproved && m.Room.Members.Any(rm => rm.UserId == currentUserId))
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (targetMaterial is null)
        {
            throw new DomainException("Chưa tìm thấy học liệu PDF phù hợp trong các phòng học của bạn để tạo đề vá lỗ hổng.");
        }

        await _roomService.EnsureUserIsRoomMemberAsync(targetMaterial.RoomId, currentUserId, isAdmin: false, cancellationToken);

        var missedQuestions = await _context.QuizAttemptAnswers
            .AsNoTracking()
            .Include(ans => ans.Question)
            .Where(ans => ans.Attempt.UserId == currentUserId && !ans.IsCorrect && ans.Attempt.Quiz.RoomId == targetMaterial.RoomId)
            .OrderByDescending(ans => ans.Attempt.CompletedAt)
            .Take(8)
            .Select(ans => $"- Câu từng sai: {ans.Question.QuestionText} (Kiến thức đúng: {ans.Question.Explanation})")
            .ToListAsync(cancellationToken);

        var focusHeader = request.FocusTopics is { Count: > 0 }
            ? $"TRỌNG TÂM VÁ LỖ HỔNG KIẾN THỨC: {string.Join("; ", request.FocusTopics.Take(3))}\n"
            : "TRỌNG TÂM VÁ LỖ HỔNG KIẾN THỨC TỪ LỊCH SỬ LÀM BÀI:\n";

        var enrichedSummary = string.Join("\n", new[]
        {
            focusHeader,
            missedQuestions.Count > 0 ? string.Join("\n", missedQuestions) : string.Empty,
            targetMaterial.AiSummary ?? string.Empty
        }.Where(s => !string.IsNullOrWhiteSpace(s)));

        var sourceText = !string.IsNullOrWhiteSpace(targetMaterial.ExtractedText)
            ? targetMaterial.ExtractedText
            : enrichedSummary;

        var creator = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == currentUserId, cancellationToken)
            ?? throw new NotFoundException("Người dùng", currentUserId);

        var aiResponse = await _aiClient.GenerateQuizAsync(
            materialTitle: $"Vá lỗ hổng: {targetMaterial.Title}",
            extractedText: sourceText.Length >= 20 ? sourceText : $"Nội dung ôn tập chuyên đề {targetMaterial.Title} môn {targetMaterial.Room.SubjectCategory}.",
            aiSummary: enrichedSummary,
            questionCount: questionCount,
            difficulty: "Medium",
            cancellationToken: cancellationToken);

        _context.AiUsageLogs.Add(new AiUsageLog
        {
            UserId = currentUserId,
            RoomId = targetMaterial.RoomId,
            FeatureType = AiFeatureType.GenerateQuiz,
            ModelName = aiResponse.Usage.ModelName,
            PromptTokens = aiResponse.Usage.PromptTokens,
            CompletionTokens = aiResponse.Usage.CompletionTokens,
            TotalTokens = aiResponse.Usage.TotalTokens,
            DurationMs = aiResponse.Usage.DurationMs,
            IsSuccess = aiResponse.Usage.IsSuccess,
            CreatedAt = DateTime.UtcNow
        });

        var title = $"[Vá lỗ hổng AI] {targetMaterial.Title}";
        var quiz = new Quiz
        {
            RoomId = targetMaterial.RoomId,
            MaterialId = targetMaterial.Id,
            CreatedById = currentUserId,
            Title = title.Length > 200 ? title[..200] : title,
            QuestionCount = aiResponse.Questions.Count,
            DurationMinutes = Math.Max(5, questionCount * 2),
            Difficulty = QuizDifficulty.Medium,
            CreatedAt = DateTime.UtcNow
        };

        var orderIdx = 1;
        foreach (var q in aiResponse.Questions)
        {
            var normalizedCorrect = q.CorrectOption.Trim().ToUpperInvariant();
            if (normalizedCorrect is not ("A" or "B" or "C" or "D"))
            {
                normalizedCorrect = "A";
            }

            quiz.Questions.Add(new QuizQuestion
            {
                OrderIndex = orderIdx++,
                QuestionText = q.QuestionText.Trim(),
                OptionA = q.OptionA.Trim(),
                OptionB = q.OptionB.Trim(),
                OptionC = q.OptionC.Trim(),
                OptionD = q.OptionD.Trim(),
                CorrectOption = normalizedCorrect,
                Explanation = q.Explanation.Trim()
            });
        }

        _context.Quizzes.Add(quiz);
        await _context.SaveChangesAsync(cancellationToken);

        await _realtimeNotifier.NotifyWorkspaceResourceUpdatedAsync(
            targetMaterial.RoomId,
            "Quiz",
            quiz.Title,
            creator.FullName,
            cancellationToken);

        return new QuizSummaryDto
        {
            Id = quiz.Id,
            RoomId = quiz.RoomId,
            MaterialId = quiz.MaterialId,
            MaterialTitle = targetMaterial.Title,
            CreatedById = currentUserId,
            CreatedByName = creator.FullName,
            Title = quiz.Title,
            QuestionCount = quiz.QuestionCount,
            DurationMinutes = quiz.DurationMinutes,
            Difficulty = quiz.Difficulty.ToString(),
            AttemptCount = 0,
            MyBestScore = null,
            CreatedAt = quiz.CreatedAt
        };
    }
}
