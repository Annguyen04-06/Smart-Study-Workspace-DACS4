using SmartStudy.Application.DTOs.Quizzes;
using SmartStudy.Application.Services;
using SmartStudy.Application.Validators;
using SmartStudy.Domain.Entities;
using SmartStudy.Domain.Enums;
using SmartStudy.UnitTests.TestInfrastructure;
using Xunit;

namespace SmartStudy.UnitTests;

/// <summary>
/// Kiểm thử đơn vị cho thuật toán chấm điểm Quiz thang 10, sinh đề AI và ghi nhận lịch sử làm bài.
/// </summary>
public class QuizServiceTests
{
    [Theory]
    [InlineData(0, 5, 0.00)]
    [InlineData(4, 5, 8.00)]
    [InlineData(5, 5, 10.00)]
    [InlineData(2, 3, 6.67)]
    [InlineData(1, 3, 3.33)]
    public void CalculateScaleTenScore_ShouldComputeAccurateTwoDecimalScore(
        int correctCount,
        int totalQuestions,
        decimal expectedScore)
    {
        var actual = QuizService.CalculateScaleTenScore(correctCount, totalQuestions);
        Assert.Equal(expectedScore, actual);
    }

    [Fact]
    public async Task GenerateAndSubmitQuiz_ShouldGradeAccurately_LogAiUsage_AndEmitRealtimeNotification()
    {
        using var context = TestDbContextFactory.CreateContext();
        var student = new User { FullName = "Nguyễn Văn An", Email = "an@edu.vn", PasswordHash = "hash" };
        context.Users.Add(student);
        await context.SaveChangesAsync();

        var roomService = new RoomService(
            context,
            new FakeFileStorageService(),
            new CreateRoomRequestValidator(),
            new JoinRoomByCodeRequestValidator());

        var room = await roomService.CreateRoomAsync(student.Id, new Application.DTOs.Rooms.CreateRoomRequestDto
        {
            Name = "Phòng Đồ thị",
            SubjectCategory = "Khoa học Máy tính"
        });

        var material = new CourseMaterial
        {
            RoomId = room.Id,
            UploadedById = student.Id,
            Title = "Bài giảng Dijkstra",
            OriginalFileName = "dijkstra.pdf",
            StoredFilePath = "/uploads/materials/dijkstra.pdf",
            FileSizeBytes = 10240,
            PageCount = 6,
            ExtractedText = "Thuật toán Dijkstra sử dụng hàng đợi ưu tiên Binary Heap với trọng số cạnh không âm.",
            AiSummary = "Tóm tắt Dijkstra",
            IsApproved = true
        };
        context.CourseMaterials.Add(material);
        await context.SaveChangesAsync();

        var notifier = new FakeRealtimeNotifier();
        var quizService = new QuizService(
            context,
            roomService,
            new FakeAiMicroserviceClient(),
            notifier,
            new GenerateQuizRequestValidator(),
            new SubmitQuizRequestValidator());

        // 1. Sinh đề 4 câu từ AI
        var generated = await quizService.GenerateQuizFromMaterialAsync(room.Id, student.Id, new GenerateQuizRequestDto
        {
            MaterialId = material.Id,
            QuestionCount = 4,
            DurationMinutes = 10,
            Difficulty = "Medium"
        });

        Assert.Equal(4, generated.QuestionCount);
        Assert.Single(notifier.Notifications);
        Assert.Equal("Quiz", notifier.Notifications[0].ResourceType);
        Assert.Equal(1, context.AiUsageLogs.Count(l => l.FeatureType == AiFeatureType.GenerateQuiz && l.IsSuccess));

        // 2. Lấy đề làm bài và nộp bài (trả lời đúng 3/4 câu -> điểm 7.50)
        var takeDto = await quizService.GetQuizForTakingAsync(generated.Id, student.Id, isAdmin: false);
        Assert.Equal(4, takeDto.Questions.Count);

        var submitResult = await quizService.SubmitQuizAttemptAsync(generated.Id, student.Id, new SubmitQuizRequestDto
        {
            TimeSpentSeconds = 120,
            Answers = new List<SubmitAnswerItemDto>
            {
                new() { QuestionId = takeDto.Questions[0].Id, SelectedOption = "B" }, // Đúng
                new() { QuestionId = takeDto.Questions[1].Id, SelectedOption = "B" }, // Đúng
                new() { QuestionId = takeDto.Questions[2].Id, SelectedOption = "b" }, // Đúng (không phân biệt hoa/thường)
                new() { QuestionId = takeDto.Questions[3].Id, SelectedOption = "A" }  // Sai
            }
        });

        Assert.Equal(3, submitResult.CorrectAnswersCount);
        Assert.Equal(4, submitResult.TotalQuestions);
        Assert.Equal(7.50m, submitResult.Score);
        Assert.Equal(4, submitResult.Details.Count);

        // 3. Phân tích lỗ hổng kiến thức AI (Weakness Radar) từ câu trả lời sai
        var weakness = await quizService.AnalyzeMyWeaknessesAsync(student.Id, new AnalyzeWeaknessRequestDto { RoomId = room.Id });
        Assert.Equal(1, weakness.TotalAttemptsAnalyzed);
        Assert.Equal(1, weakness.TotalWrongAnswers);
        Assert.Equal(75.0, weakness.OverallAccuracyPercent);
        Assert.Single(weakness.SubjectRadar);
        Assert.NotEmpty(weakness.TopWeaknesses);
        Assert.Equal(room.Id, weakness.SuggestedRoomId);
        Assert.Equal(material.Id, weakness.SuggestedMaterialId);
        Assert.Equal(1, context.AiUsageLogs.Count(l => l.FeatureType == AiFeatureType.AnalyzeWeakness && l.IsSuccess));

        // 4. Tạo nhanh đề Quiz 5 câu vá lỗ hổng kiến thức
        var remedial = await quizService.GenerateRemedialQuizAsync(student.Id, new GenerateRemedialQuizRequestDto
        {
            RoomId = weakness.SuggestedRoomId,
            MaterialId = weakness.SuggestedMaterialId,
            FocusTopics = weakness.TopWeaknesses.Select(w => w.TopicName).ToList(),
            QuestionCount = 5
        });
        Assert.Equal(5, remedial.QuestionCount);
        Assert.Contains("Vá lỗ hổng AI", remedial.Title);
    }
}
