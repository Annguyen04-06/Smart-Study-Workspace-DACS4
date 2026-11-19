using Microsoft.EntityFrameworkCore;
using SmartStudy.Application.Services;
using SmartStudy.Application.Validators;
using SmartStudy.Domain.Entities;
using SmartStudy.Domain.Enums;
using SmartStudy.Infrastructure.Persistence;
using SmartStudy.UnitTests.TestInfrastructure;
using Xunit;

namespace SmartStudy.UnitTests;

/// <summary>
/// Kiểm thử Trợ giảng @bot Streaming: phát đúng thứ tự từng cụm chữ, lưu toàn văn + AiUsageLogs,
/// tự chuyển sang endpoint không stream khi luồng bị ngắt và Delta Sync tin nhắn mới.
/// </summary>
public class ChatServiceStreamingTests
{
    private static ChatService CreateChatService(AppDbContext context, FakeAiMicroserviceClient aiClient)
    {
        var roomService = new RoomService(
            context,
            new FakeFileStorageService(),
            new CreateRoomRequestValidator(),
            new JoinRoomByCodeRequestValidator());
        return new ChatService(context, roomService, aiClient, new SendMessageRequestValidator());
    }

    private static async Task<(User Student, StudyRoom Room)> SeedRoomWithMemberAsync(AppDbContext context)
    {
        var student = new User { FullName = "Nguyễn Văn An", Email = "an@vku.udn.vn", PasswordHash = "hash", Role = UserRole.Student };
        context.Users.Add(student);
        await context.SaveChangesAsync();

        var room = new StudyRoom
        {
            Name = "Cấu trúc Dữ liệu",
            SubjectCategory = "Khoa học Máy tính",
            InviteCode = "ABCD2345",
            CreatedById = student.Id
        };
        context.StudyRooms.Add(room);
        await context.SaveChangesAsync();

        context.RoomMembers.Add(new RoomMember { RoomId = room.Id, UserId = student.Id, RoleInRoom = RoomMemberRole.Owner });
        await context.SaveChangesAsync();
        return (student, room);
    }

    [Fact]
    public async Task StreamAndSaveBotReply_WhenStreamSucceeds_ShouldEmitChunksInOrderAndPersistFullAnswer()
    {
        using var context = TestDbContextFactory.CreateContext();
        var (student, room) = await SeedRoomWithMemberAsync(context);
        var aiClient = new FakeAiMicroserviceClient();
        var service = CreateChatService(context, aiClient);
        var receivedChunks = new List<string>();

        var result = await service.StreamAndSaveBotReplyAsync(
            room.Id,
            student.Id,
            "@bot Dijkstra là gì?",
            chunk =>
            {
                receivedChunks.Add(chunk);
                return Task.CompletedTask;
            });

        Assert.Equal(aiClient.StreamChunks, receivedChunks);
        Assert.Null(result.StreamFallbackReason);
        Assert.True(result.Message.IsAiBot);
        Assert.Equal(string.Concat(aiClient.StreamChunks), result.Message.Content);

        var savedBotMessage = await context.ChatMessages.SingleAsync(m => m.IsAiBot);
        Assert.Equal(result.Message.Id, savedBotMessage.Id);
        Assert.Equal(result.Message.Content, savedBotMessage.Content);

        var usageLog = await context.AiUsageLogs.SingleAsync();
        Assert.Equal(AiFeatureType.BotChat, usageLog.FeatureType);
        Assert.Equal(420, usageLog.TotalTokens);
        Assert.True(usageLog.IsSuccess);
    }

    [Fact]
    public async Task StreamAndSaveBotReply_WhenStreamBreaksMidway_ShouldFallbackToNonStreamingAnswer()
    {
        using var context = TestDbContextFactory.CreateContext();
        var (student, room) = await SeedRoomWithMemberAsync(context);
        var aiClient = new FakeAiMicroserviceClient { BreakStreamMidway = true };
        var service = CreateChatService(context, aiClient);

        var result = await service.StreamAndSaveBotReplyAsync(
            room.Id,
            student.Id,
            "@bot Dijkstra là gì?",
            _ => Task.CompletedTask);

        Assert.NotNull(result.StreamFallbackReason);
        Assert.Equal(FakeAiMicroserviceClient.NonStreamingAnswer, result.Message.Content);

        var usageLog = await context.AiUsageLogs.SingleAsync();
        Assert.Equal(500, usageLog.TotalTokens);
        Assert.True(usageLog.IsSuccess);
    }

    [Fact]
    public async Task GetRoomMessagesAfter_ShouldReturnOnlyNewerMessagesInAscendingOrder()
    {
        using var context = TestDbContextFactory.CreateContext();
        var (student, room) = await SeedRoomWithMemberAsync(context);
        var baseTime = DateTime.UtcNow.AddMinutes(-10);
        for (var i = 1; i <= 5; i++)
        {
            context.ChatMessages.Add(new ChatMessage
            {
                RoomId = room.Id,
                SenderId = student.Id,
                Content = $"Tin nhắn số {i}",
                CreatedAt = baseTime.AddMinutes(i)
            });
        }
        await context.SaveChangesAsync();

        var allIds = await context.ChatMessages.OrderBy(m => m.Id).Select(m => m.Id).ToListAsync();
        var service = CreateChatService(context, new FakeAiMicroserviceClient());

        var delta = await service.GetRoomMessagesAfterAsync(room.Id, student.Id, isAdmin: false, afterId: allIds[1], maxCount: 50);

        Assert.Equal(allIds.Skip(2), delta.Select(m => m.Id));
        Assert.Equal("Tin nhắn số 3", delta[0].Content);
        Assert.Equal("Nguyễn Văn An", delta[0].SenderName);
    }
}
