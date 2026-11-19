using System.Collections.Concurrent;

namespace SmartStudy.API.Hubs;

public class WhiteboardPointDto
{
    public double X { get; set; }
    public double Y { get; set; }
}

public class WhiteboardStrokeDto
{
    public string Id { get; set; } = string.Empty;
    public string Tool { get; set; } = "pen";
    public string Color { get; set; } = "#4f46e5";
    public double Width { get; set; } = 3;
    public List<WhiteboardPointDto> Points { get; set; } = new();
    public int AuthorUserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
}

/// <summary>
/// Singleton quản lý trạng thái Bảng trắng Học nhóm Trực tiếp (Collaborative Whiteboard) trong bộ nhớ theo từng phòng học.
/// Lưu tối đa 600 nét vẽ gần nhất mỗi phòng để thành viên vào sau thấy ngay nội dung bảng, kèm bảng màu con trỏ Figma.
/// </summary>
public class RoomWhiteboardTracker
{
    private const int MaxStrokesPerRoom = 600;

    private static readonly string[] CursorPalette =
    [
        "#6366f1", // Indigo
        "#ec4899", // Pink
        "#10b981", // Emerald
        "#f59e0b", // Amber
        "#06b6d4", // Cyan
        "#8b5cf6", // Violet
        "#ef4444", // Rose
        "#14b8a6"  // Teal
    ];

    private readonly ConcurrentDictionary<int, List<WhiteboardStrokeDto>> _roomStrokes = new();

    public static string GetUserCursorColor(int userId)
    {
        var idx = Math.Abs(userId) % CursorPalette.Length;
        return CursorPalette[idx];
    }

    public IReadOnlyList<WhiteboardStrokeDto> GetStrokes(int roomId)
    {
        if (!_roomStrokes.TryGetValue(roomId, out var list))
        {
            return Array.Empty<WhiteboardStrokeDto>();
        }

        lock (list)
        {
            return list.ToList();
        }
    }

    public WhiteboardStrokeDto AppendStroke(int roomId, WhiteboardStrokeDto stroke)
    {
        var list = _roomStrokes.GetOrAdd(roomId, _ => new List<WhiteboardStrokeDto>());
        lock (list)
        {
            if (list.Count >= MaxStrokesPerRoom)
            {
                list.RemoveRange(0, list.Count - MaxStrokesPerRoom + 1);
            }
            list.Add(stroke);
        }
        return stroke;
    }

    public void ClearRoom(int roomId)
    {
        if (_roomStrokes.TryGetValue(roomId, out var list))
        {
            lock (list)
            {
                list.Clear();
            }
        }
    }
}
