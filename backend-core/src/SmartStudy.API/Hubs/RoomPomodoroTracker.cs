using System.Collections.Concurrent;

namespace SmartStudy.API.Hubs;

/// <summary>
/// Trạng thái đồng hồ Pomodoro học nhóm thời gian thực truyền tới các tab trình duyệt qua SignalR.
/// </summary>
public record RoomPomodoroStateDto(
    int RoomId,
    string Mode,
    int DurationSeconds,
    string Status,
    int RemainingSeconds,
    DateTime? EndTimeUtc,
    string StartedByName,
    DateTime ServerNowUtc
);

/// <summary>
/// Singleton quản lý trạng thái Đồng hồ Pomodoro Học Nhóm trên bộ nhớ cho từng phòng học (Focus 25p | ShortBreak 5p).
/// Đảm bảo mọi thành viên mở tab ở bất kỳ thời điểm nào đều đồng bộ chính xác từng giây.
/// </summary>
public class RoomPomodoroTracker
{
    private sealed class MutablePomodoroEntry
    {
        public string Mode { get; set; } = "Focus";
        public int DurationSeconds { get; set; } = 25 * 60;
        public bool IsRunning { get; set; }
        public int PausedRemainingSeconds { get; set; } = 25 * 60;
        public DateTime? EndTimeUtc { get; set; }
        public string StartedByName { get; set; } = "Hệ thống";
    }

    private readonly ConcurrentDictionary<int, MutablePomodoroEntry> _states = new();
    private readonly object _sync = new();

    public RoomPomodoroStateDto GetState(int roomId)
    {
        lock (_sync)
        {
            var entry = _states.GetOrAdd(roomId, _ => new MutablePomodoroEntry());
            return ToSnapshotUnsafe(roomId, entry);
        }
    }

    public RoomPomodoroStateDto Start(int roomId, int modeMinutes, string startedByName)
    {
        lock (_sync)
        {
            var clampedMinutes = modeMinutes is 5 or 15 or 25 or 45 ? modeMinutes : (modeMinutes <= 10 ? 5 : 25);
            var modeName = clampedMinutes <= 10 ? "ShortBreak" : "Focus";
            var totalSec = clampedMinutes * 60;

            var entry = _states.GetOrAdd(roomId, _ => new MutablePomodoroEntry());

            // Nếu đang tạm dừng ở đúng cùng chế độ và còn thời gian -> tiếp tục chạy từ số giây còn lại
            var resumeSeconds = !entry.IsRunning &&
                                entry.Mode == modeName &&
                                entry.PausedRemainingSeconds > 0 &&
                                entry.PausedRemainingSeconds < totalSec
                ? entry.PausedRemainingSeconds
                : totalSec;

            entry.Mode = modeName;
            entry.DurationSeconds = totalSec;
            entry.IsRunning = true;
            entry.EndTimeUtc = DateTime.UtcNow.AddSeconds(resumeSeconds);
            entry.StartedByName = startedByName;

            return ToSnapshotUnsafe(roomId, entry);
        }
    }

    public RoomPomodoroStateDto Pause(int roomId, string actorName)
    {
        lock (_sync)
        {
            var entry = _states.GetOrAdd(roomId, _ => new MutablePomodoroEntry());
            if (entry.IsRunning && entry.EndTimeUtc.HasValue)
            {
                var remaining = (int)Math.Max(0, Math.Ceiling((entry.EndTimeUtc.Value - DateTime.UtcNow).TotalSeconds));
                entry.PausedRemainingSeconds = remaining;
                entry.IsRunning = false;
                entry.EndTimeUtc = null;
                entry.StartedByName = actorName;
            }
            return ToSnapshotUnsafe(roomId, entry);
        }
    }

    public RoomPomodoroStateDto Reset(int roomId, int modeMinutes, string actorName)
    {
        lock (_sync)
        {
            var clampedMinutes = modeMinutes <= 10 ? 5 : 25;
            var modeName = clampedMinutes <= 10 ? "ShortBreak" : "Focus";
            var totalSec = clampedMinutes * 60;

            var entry = _states.GetOrAdd(roomId, _ => new MutablePomodoroEntry());
            entry.Mode = modeName;
            entry.DurationSeconds = totalSec;
            entry.IsRunning = false;
            entry.PausedRemainingSeconds = totalSec;
            entry.EndTimeUtc = null;
            entry.StartedByName = actorName;

            return ToSnapshotUnsafe(roomId, entry);
        }
    }

    private static RoomPomodoroStateDto ToSnapshotUnsafe(int roomId, MutablePomodoroEntry entry)
    {
        var now = DateTime.UtcNow;
        int remaining;
        if (entry.IsRunning && entry.EndTimeUtc.HasValue)
        {
            remaining = (int)Math.Max(0, Math.Ceiling((entry.EndTimeUtc.Value - now).TotalSeconds));
            if (remaining <= 0)
            {
                entry.IsRunning = false;
                entry.EndTimeUtc = null;
                entry.PausedRemainingSeconds = entry.DurationSeconds;
                remaining = entry.DurationSeconds;
            }
        }
        else
        {
            remaining = entry.PausedRemainingSeconds;
        }

        return new RoomPomodoroStateDto(
            RoomId: roomId,
            Mode: entry.Mode,
            DurationSeconds: entry.DurationSeconds,
            Status: entry.IsRunning ? "Running" : "Paused",
            RemainingSeconds: remaining,
            EndTimeUtc: entry.IsRunning ? entry.EndTimeUtc : null,
            StartedByName: entry.StartedByName,
            ServerNowUtc: now);
    }
}
