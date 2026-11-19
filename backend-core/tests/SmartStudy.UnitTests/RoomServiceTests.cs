using SmartStudy.Application.DTOs.Rooms;
using SmartStudy.Application.Services;
using SmartStudy.Application.Validators;
using SmartStudy.Domain.Entities;
using SmartStudy.Domain.Enums;
using SmartStudy.Domain.Exceptions;
using SmartStudy.UnitTests.TestInfrastructure;
using Xunit;

namespace SmartStudy.UnitTests;

/// <summary>
/// Kiểm thử đơn vị cho nghiệp vụ Phòng học: sinh mã mời InviteCode, phân quyền thành viên và bảo vệ Owner.
/// </summary>
public class RoomServiceTests
{
    private static RoomService CreateRoomService(Infrastructure.Persistence.AppDbContext context)
    {
        return new RoomService(
            context,
            new FakeFileStorageService(),
            new CreateRoomRequestValidator(),
            new JoinRoomByCodeRequestValidator());
    }

    [Fact]
    public void GenerateInviteCode_ShouldReturn8UppercaseAlphanumericChars_WithoutConfusingChars()
    {
        using var context = TestDbContextFactory.CreateContext();
        var service = CreateRoomService(context);

        var code1 = service.GenerateInviteCode();
        var code2 = service.GenerateInviteCode();

        Assert.Equal(8, code1.Length);
        Assert.Matches("^[A-Z2-9]{8}$", code1);
        Assert.NotEqual(code1, code2);
    }

    [Fact]
    public async Task CreateRoom_And_JoinByInviteCode_ShouldAssignOwnerAndMemberCorrectly()
    {
        using var context = TestDbContextFactory.CreateContext();
        var owner = new User { FullName = "Nguyễn Văn An", Email = "an@edu.vn", PasswordHash = "hash", Role = UserRole.Student };
        var joiner = new User { FullName = "Trần Quốc Bình", Email = "binh@edu.vn", PasswordHash = "hash", Role = UserRole.Student };
        context.Users.AddRange(owner, joiner);
        await context.SaveChangesAsync();

        var service = CreateRoomService(context);

        var createdRoom = await service.CreateRoomAsync(owner.Id, new CreateRoomRequestDto
        {
            Name = "Cấu trúc Dữ liệu",
            SubjectCategory = "Khoa học Máy tính",
            Description = "Ôn thi cuối kỳ"
        });

        Assert.Equal("Active", createdRoom.Status);
        Assert.Equal("Owner", createdRoom.MyRoleInRoom);
        Assert.Equal(1, createdRoom.MemberCount);

        // Sinh viên 2 tham gia bằng mã mời có tiền tố '#' và khoảng trắng chữ thường vẫn phải khớp chính xác
        var joinedRoom = await service.JoinRoomByInviteCodeAsync(joiner.Id, new JoinRoomByCodeRequestDto
        {
            InviteCode = $"  #{createdRoom.InviteCode.ToLowerInvariant()}  "
        });

        Assert.Equal(createdRoom.Id, joinedRoom.Id);
        Assert.Equal("Member", joinedRoom.MyRoleInRoom);
        Assert.Equal(2, joinedRoom.MemberCount);
    }

    [Fact]
    public async Task EnsureUserIsRoomMember_WhenOutsiderAccessesRoom_ShouldThrowForbiddenAccessException()
    {
        using var context = TestDbContextFactory.CreateContext();
        var owner = new User { FullName = "Owner", Email = "owner@edu.vn", PasswordHash = "h" };
        var outsider = new User { FullName = "Outsider", Email = "outsider@edu.vn", PasswordHash = "h" };
        context.Users.AddRange(owner, outsider);
        await context.SaveChangesAsync();

        var service = CreateRoomService(context);
        var room = await service.CreateRoomAsync(owner.Id, new CreateRoomRequestDto
        {
            Name = "Mạng Máy tính",
            SubjectCategory = "Mạng & Truyền thông"
        });

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            service.EnsureUserIsRoomMemberAsync(room.Id, outsider.Id, isAdmin: false));
    }

    [Fact]
    public async Task LeaveRoom_WhenCallerIsOwner_ShouldThrowDomainException()
    {
        using var context = TestDbContextFactory.CreateContext();
        var owner = new User { FullName = "Owner", Email = "owner@edu.vn", PasswordHash = "h" };
        context.Users.Add(owner);
        await context.SaveChangesAsync();

        var service = CreateRoomService(context);
        var room = await service.CreateRoomAsync(owner.Id, new CreateRoomRequestDto
        {
            Name = "Hệ điều hành",
            SubjectCategory = "Khoa học Máy tính"
        });

        await Assert.ThrowsAsync<DomainException>(() =>
            service.LeaveRoomAsync(room.Id, owner.Id));
    }

    [Fact]
    public async Task GetRoomLeaderboard_And_TogglePinMessage_ShouldRankMembers_AndUpdatePinFlag()
    {
        using var context = TestDbContextFactory.CreateContext();
        var u1 = new User { StudentCode = "23IT001", FullName = "Nguyễn Văn An", Email = "an@vku.udn.vn", PasswordHash = "h" };
        var u2 = new User { StudentCode = "23IT002", FullName = "Trần Thị Bình", Email = "binh@vku.udn.vn", PasswordHash = "h" };
        context.Users.AddRange(u1, u2);
        await context.SaveChangesAsync();

        var roomService = CreateRoomService(context);
        var room = await roomService.CreateRoomAsync(u1.Id, new CreateRoomRequestDto
        {
            Name = "Lập trình Mạng",
            SubjectCategory = "Lập trình Mạng"
        });
        await roomService.JoinRoomByIdAsync(room.Id, u2.Id);

        var chatService = new ChatService(
            context,
            roomService,
            new FakeAiMicroserviceClient(),
            new SendMessageRequestValidator());

        var sent = await chatService.SaveStudentMessageAsync(room.Id, u2.Id, new Application.DTOs.Chat.SendMessageRequestDto
        {
            Content = "Công thức cửa sổ TCP rất quan trọng!"
        });
        Assert.False(sent.IsPinned);

        var toggled = await chatService.TogglePinMessageAsync(room.Id, sent.Id, u1.Id, isAdmin: false);
        Assert.True(toggled.IsPinned);

        var pinnedList = await chatService.GetPinnedMessagesAsync(room.Id, u1.Id, isAdmin: false);
        Assert.Single(pinnedList);

        var leaderboard = await roomService.GetRoomLeaderboardAsync(room.Id, u1.Id, isAdmin: false);
        Assert.Equal(2, leaderboard.Count);
        // u2 gửi 1 tin nhắn học thuật (+2 điểm) nên xếp hạng 1
        Assert.Equal(u2.Id, leaderboard[0].UserId);
        Assert.Equal(1, leaderboard[0].Rank);
    }
}
