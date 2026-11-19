using Microsoft.AspNetCore.SignalR;
using SmartStudy.Application.Common.Interfaces;

namespace SmartStudy.API.Hubs;

/// <summary>
/// Thực thi IStudyRoomRealtimeNotifier để bắn sự kiện WorkspaceResourceUpdated tới các thành viên trong phòng
/// ngay khi có tài liệu PDF, đề Quiz hoặc bộ thẻ Flashcard mới vừa được tạo.
/// </summary>
public class SignalRRealtimeNotifier : IStudyRoomRealtimeNotifier
{
    private readonly IHubContext<StudyRoomHub> _hubContext;

    public SignalRRealtimeNotifier(IHubContext<StudyRoomHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyWorkspaceResourceUpdatedAsync(
        int roomId,
        string resourceType,
        string resourceTitle,
        string actorName,
        CancellationToken cancellationToken = default)
    {
        var groupName = StudyRoomHub.GetRoomGroupName(roomId);
        await _hubContext.Clients.Group(groupName).SendAsync(
            "WorkspaceResourceUpdated",
            new
            {
                roomId,
                resourceType,
                resourceTitle,
                actorName,
                timestamp = DateTime.UtcNow
            },
            cancellationToken);
    }
}
