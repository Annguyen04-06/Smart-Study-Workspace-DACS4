namespace SmartStudy.Application.Common.Interfaces;

/// <summary>
/// Hợp đồng bắn thông báo thời gian thực qua SignalR từ các Business Services ở tầng Application
/// mà không làm tầng Application bị phụ thuộc vào thư viện Microsoft.AspNetCore.SignalR.
/// </summary>
public interface IStudyRoomRealtimeNotifier
{
    Task NotifyWorkspaceResourceUpdatedAsync(
        int roomId,
        string resourceType,
        string resourceTitle,
        string actorName,
        CancellationToken cancellationToken = default);
}
