using System.Collections.Concurrent;
using SmartStudy.Application.DTOs.Chat;

namespace SmartStudy.API.Hubs;

/// <summary>
/// Quản lý Thread-Safe danh sách kết nối WebSocket trực tuyến theo từng phòng học.
/// Hỗ trợ trường hợp một sinh viên mở nhiều tab trình duyệt cùng lúc mà không làm sai lệch trạng thái Online.
/// </summary>
public class PresenceTracker
{
    private sealed record ConnectedMemberEntry(
        int UserId,
        string FullName,
        string? AvatarUrl,
        HashSet<string> ConnectionIds);

    // RoomId -> (UserId -> ConnectedMemberEntry)
    private readonly ConcurrentDictionary<int, Dictionary<int, ConnectedMemberEntry>> _roomPresence = new();
    // ConnectionId -> Danh sách RoomId đang tham gia
    private readonly ConcurrentDictionary<string, HashSet<int>> _connectionRooms = new();
    private readonly object _syncLock = new();

    public IReadOnlyList<OnlineRoomUserDto> UserConnectedToRoom(
        int roomId,
        int userId,
        string fullName,
        string? avatarUrl,
        string connectionId)
    {
        lock (_syncLock)
        {
            if (!_roomPresence.TryGetValue(roomId, out var usersInRoom))
            {
                usersInRoom = new Dictionary<int, ConnectedMemberEntry>();
                _roomPresence[roomId] = usersInRoom;
            }

            if (!usersInRoom.TryGetValue(userId, out var entry))
            {
                entry = new ConnectedMemberEntry(userId, fullName, avatarUrl, new HashSet<string>());
                usersInRoom[userId] = entry;
            }
            else
            {
                usersInRoom[userId] = entry with { FullName = fullName, AvatarUrl = avatarUrl };
                entry = usersInRoom[userId];
            }

            entry.ConnectionIds.Add(connectionId);

            if (!_connectionRooms.TryGetValue(connectionId, out var joinedRooms))
            {
                joinedRooms = new HashSet<int>();
                _connectionRooms[connectionId] = joinedRooms;
            }
            joinedRooms.Add(roomId);

            return SnapshotOnlineUsersUnsafe(roomId);
        }
    }

    public IReadOnlyList<OnlineRoomUserDto> UserLeftRoom(int roomId, int userId, string connectionId)
    {
        lock (_syncLock)
        {
            RemoveConnectionFromRoomUnsafe(roomId, userId, connectionId);

            if (_connectionRooms.TryGetValue(connectionId, out var joinedRooms))
            {
                joinedRooms.Remove(roomId);
                if (joinedRooms.Count == 0)
                {
                    _connectionRooms.TryRemove(connectionId, out _);
                }
            }

            return SnapshotOnlineUsersUnsafe(roomId);
        }
    }

    public IReadOnlyDictionary<int, IReadOnlyList<OnlineRoomUserDto>> ConnectionClosed(int userId, string connectionId)
    {
        var affectedRooms = new Dictionary<int, IReadOnlyList<OnlineRoomUserDto>>();

        lock (_syncLock)
        {
            if (!_connectionRooms.TryRemove(connectionId, out var joinedRooms))
            {
                return affectedRooms;
            }

            foreach (var roomId in joinedRooms)
            {
                RemoveConnectionFromRoomUnsafe(roomId, userId, connectionId);
                affectedRooms[roomId] = SnapshotOnlineUsersUnsafe(roomId);
            }
        }

        return affectedRooms;
    }

    public IReadOnlyList<OnlineRoomUserDto> GetOnlineUsers(int roomId)
    {
        lock (_syncLock)
        {
            return SnapshotOnlineUsersUnsafe(roomId);
        }
    }

    private void RemoveConnectionFromRoomUnsafe(int roomId, int userId, string connectionId)
    {
        if (!_roomPresence.TryGetValue(roomId, out var usersInRoom))
        {
            return;
        }

        if (usersInRoom.TryGetValue(userId, out var entry))
        {
            entry.ConnectionIds.Remove(connectionId);
            if (entry.ConnectionIds.Count == 0)
            {
                usersInRoom.Remove(userId);
            }
        }

        if (usersInRoom.Count == 0)
        {
            _roomPresence.TryRemove(roomId, out _);
        }
    }

    private IReadOnlyList<OnlineRoomUserDto> SnapshotOnlineUsersUnsafe(int roomId)
    {
        if (!_roomPresence.TryGetValue(roomId, out var usersInRoom))
        {
            return Array.Empty<OnlineRoomUserDto>();
        }

        return usersInRoom.Values
            .OrderBy(u => u.FullName)
            .Select(u => new OnlineRoomUserDto
            {
                UserId = u.UserId,
                FullName = u.FullName,
                AvatarUrl = u.AvatarUrl
            })
            .ToList();
    }
}
