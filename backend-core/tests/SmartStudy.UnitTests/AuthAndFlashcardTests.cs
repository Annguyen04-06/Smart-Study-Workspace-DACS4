using Microsoft.Extensions.Options;
using SmartStudy.Application.DTOs.Auth;
using SmartStudy.Application.DTOs.Flashcards;
using SmartStudy.Application.Services;
using SmartStudy.Application.Validators;
using SmartStudy.Domain.Entities;
using SmartStudy.Domain.Enums;
using SmartStudy.Domain.Exceptions;
using SmartStudy.Infrastructure.Identity;
using SmartStudy.UnitTests.TestInfrastructure;
using Xunit;

namespace SmartStudy.UnitTests;

/// <summary>
/// Kiểm thử đơn vị cho Xác thực BCrypt/JWT, kiểm tra khóa tài khoản và Spaced Repetition trên Flashcards.
/// </summary>
public class AuthAndFlashcardTests
{
    private static AuthService CreateAuthService(
        Infrastructure.Persistence.AppDbContext context,
        FakeEmailSender? emailSender = null)
    {
        var jwtOptions = Options.Create(new JwtSettings
        {
            SecretKey = "UnitTest_SecretSigningKey_AtLeast32BytesLong_2026!",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpiryMinutes = 60
        });

        return new AuthService(
            context,
            new PasswordHasher(),
            new JwtTokenGenerator(jwtOptions),
            new FakeFileStorageService(),
            new RegisterRequestValidator(),
            new LoginRequestValidator(),
            new UpdateProfileRequestValidator(),
            new ChangePasswordRequestValidator(),
            emailSender);
    }

    [Fact]
    public async Task RegisterAndLogin_ShouldIssueValidJwt_AndBlockLockedAccount()
    {
        using var context = TestDbContextFactory.CreateContext();
        var authService = CreateAuthService(context);

        var registered = await authService.RegisterAsync(new RegisterRequestDto
        {
            StudentCode = "SV2026999",
            FullName = "Lê Hoàng Nam",
            Email = "nam.le@student.edu.vn",
            Password = "Password@123",
            ConfirmPassword = "Password@123"
        });

        Assert.False(string.IsNullOrWhiteSpace(registered.Token));
        Assert.Equal("SV2026999", registered.User.StudentCode);

        // Khóa tài khoản và xác nhận Login ném ForbiddenAccessException (HTTP 403)
        var entity = context.Users.Single(u => u.Id == registered.User.Id);
        entity.IsLocked = true;
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            authService.LoginAsync(new LoginRequestDto
            {
                Email = "nam.le@student.edu.vn",
                Password = "Password@123"
            }));
    }

    [Fact]
    public async Task FlashcardSpacedRepetition_ShouldUpdateMasteryLevel_AndValidateRange()
    {
        using var context = TestDbContextFactory.CreateContext();
        var student = new User { FullName = "Sinh viên Test", Email = "sv@edu.vn", PasswordHash = "h" };
        context.Users.Add(student);
        await context.SaveChangesAsync();

        var roomService = new RoomService(
            context,
            new FakeFileStorageService(),
            new CreateRoomRequestValidator(),
            new JoinRoomByCodeRequestValidator());

        var room = await roomService.CreateRoomAsync(student.Id, new Application.DTOs.Rooms.CreateRoomRequestDto
        {
            Name = "Phòng Kiến trúc Phần mềm",
            SubjectCategory = "Kỹ thuật Phần mềm"
        });

        var material = new CourseMaterial
        {
            RoomId = room.Id,
            UploadedById = student.Id,
            Title = "SOLID Principles",
            OriginalFileName = "solid.pdf",
            StoredFilePath = "/uploads/materials/solid.pdf",
            FileSizeBytes = 2048,
            PageCount = 3,
            ExtractedText = "Nguyên lý SOLID gồm SRP, OCP, LSP, ISP và DIP trong thiết kế hướng đối tượng.",
            IsApproved = true
        };
        context.CourseMaterials.Add(material);
        await context.SaveChangesAsync();

        var flashcardService = new FlashcardService(
            context,
            roomService,
            new FakeAiMicroserviceClient(),
            new FakeRealtimeNotifier(),
            new GenerateFlashcardDeckRequestValidator(),
            new UpdateFlashcardMasteryRequestValidator());

        var deck = await flashcardService.GenerateDeckFromMaterialAsync(room.Id, student.Id, new GenerateFlashcardDeckRequestDto
        {
            MaterialId = material.Id,
            CardCount = 4
        });

        Assert.Equal(4, deck.Cards.Count);
        var firstCardId = deck.Cards[0].Id;

        var updatedCard = await flashcardService.UpdateCardMasteryAsync(firstCardId, student.Id, new UpdateFlashcardMasteryRequestDto
        {
            MasteryLevel = 2
        });
        Assert.Equal(2, updatedCard.MasteryLevel);

        await Assert.ThrowsAsync<DomainException>(() =>
            flashcardService.UpdateCardMasteryAsync(firstCardId, student.Id, new UpdateFlashcardMasteryRequestDto
            {
                MasteryLevel = 5
            }));
    }

    [Fact]
    public async Task ForgotPassword_VerifyOtp_And_ResetPassword_ShouldAuthenticateWithNewPassword()
    {
        using var context = TestDbContextFactory.CreateContext();
        var fakeEmailSender = new FakeEmailSender();
        var authService = CreateAuthService(context, fakeEmailSender);

        await authService.RegisterAsync(new RegisterRequestDto
        {
            StudentCode = "23IT888",
            FullName = "Võ Thảo Vy",
            Email = "vy.test@vku.udn.vn",
            Password = "OldPassword@123",
            ConfirmPassword = "OldPassword@123"
        });

        var resetResp = await authService.RequestPasswordResetOtpAsync(new ForgotPasswordRequestDto
        {
            Email = "vy.test@vku.udn.vn"
        });

        Assert.False(string.IsNullOrWhiteSpace(resetResp.MaskedEmail));
        Assert.NotNull(fakeEmailSender.LastSentOtpCode);
        Assert.Equal(6, fakeEmailSender.LastSentOtpCode!.Length);

        await authService.VerifyPasswordResetOtpAsync(new VerifyResetOtpRequestDto
        {
            Email = "vy.test@vku.udn.vn",
            OtpCode = fakeEmailSender.LastSentOtpCode!
        });

        await authService.ResetPasswordWithOtpAsync(new ResetPasswordWithOtpRequestDto
        {
            Email = "vy.test@vku.udn.vn",
            OtpCode = fakeEmailSender.LastSentOtpCode!,
            NewPassword = "NewSecurePassword@456",
            ConfirmNewPassword = "NewSecurePassword@456"
        });

        var loginResult = await authService.LoginAsync(new LoginRequestDto
        {
            Email = "vy.test@vku.udn.vn",
            Password = "NewSecurePassword@456"
        });

        Assert.False(string.IsNullOrWhiteSpace(loginResult.Token));
    }

    [Fact]
    public async Task GenerateMindmap_And_ToggleMessageReaction_ShouldPersistAndSyncCorrectly()
    {
        using var context = TestDbContextFactory.CreateContext();
        var student = new User { FullName = "Nguyễn Văn An", Email = "an@vku.udn.vn", PasswordHash = "hash" };
        context.Users.Add(student);
        await context.SaveChangesAsync();

        var roomService = new RoomService(
            context,
            new FakeFileStorageService(),
            new CreateRoomRequestValidator(),
            new JoinRoomByCodeRequestValidator());

        var room = await roomService.CreateRoomAsync(student.Id, new Application.DTOs.Rooms.CreateRoomRequestDto
        {
            Name = "Phòng Học Sơ đồ Tư duy",
            SubjectCategory = "Trí tuệ nhân tạo"
        });

        var material = new CourseMaterial
        {
            RoomId = room.Id,
            UploadedById = student.Id,
            Title = "Giải thuật Tìm kiếm A*",
            OriginalFileName = "astar.pdf",
            StoredFilePath = "/uploads/materials/astar.pdf",
            FileSizeBytes = 4096,
            PageCount = 4,
            ExtractedText = "Giải thuật A* sử dụng hàm đánh giá f(n) = g(n) + h(n) tối ưu đường đi.",
            IsApproved = true
        };
        context.CourseMaterials.Add(material);
        await context.SaveChangesAsync();

        var materialService = new MaterialService(
            context,
            roomService,
            new FakeFileStorageService(),
            new FakeAiMicroserviceClient(),
            new FakeRealtimeNotifier(),
            new UploadMaterialMetadataValidator());

        var mindmap = await materialService.GenerateMaterialMindmapAsync(material.Id, student.Id, isAdmin: false);
        Assert.Contains("flowchart TD", mindmap.MermaidCode);
        Assert.False(string.IsNullOrWhiteSpace(context.CourseMaterials.Single(m => m.Id == material.Id).AiMindmap));

        var chatService = new ChatService(
            context,
            roomService,
            new FakeAiMicroserviceClient(),
            new SendMessageRequestValidator());

        var savedMsg = await chatService.SaveStudentMessageAsync(room.Id, student.Id, new Application.DTOs.Chat.SendMessageRequestDto
        {
            Content = "Sơ đồ tư duy A* trực quan quá!"
        });

        var reacted = await chatService.ToggleMessageReactionAsync(room.Id, savedMsg.Id, student.Id, "🔥", isAdmin: false);
        Assert.True(reacted.Reactions.ContainsKey("🔥"));
        Assert.Contains(student.Id, reacted.Reactions["🔥"]);

        var unreacted = await chatService.ToggleMessageReactionAsync(room.Id, savedMsg.Id, student.Id, "🔥", isAdmin: false);
        Assert.False(unreacted.Reactions.ContainsKey("🔥"));
    }
}
