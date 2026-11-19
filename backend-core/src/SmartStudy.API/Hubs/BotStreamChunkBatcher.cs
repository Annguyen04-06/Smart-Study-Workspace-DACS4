using System.Text;

namespace SmartStudy.API.Hubs;

/// <summary>
/// Gom các cụm chữ @bot theo nhịp cố định (~50ms) trước khi phát qua SignalR:
/// chữ hiện liên tục như ChatGPT nhưng không spam hàng trăm gói tin nhỏ tới mọi thành viên trong phòng.
/// Chỉ vòng lặp nền phát dữ liệu trong lúc stream, nên thứ tự các cụm chữ luôn được giữ nguyên.
/// </summary>
public sealed class BotStreamChunkBatcher : IAsyncDisposable
{
    private readonly StringBuilder _pending = new();
    private readonly object _gate = new();
    private readonly Func<string, Task> _publish;
    private readonly ILogger _logger;
    private readonly PeriodicTimer _timer;
    private readonly Task _flushLoop;
    private bool _isCompleted;

    public BotStreamChunkBatcher(TimeSpan interval, Func<string, Task> publish, ILogger logger)
    {
        _publish = publish;
        _logger = logger;
        _timer = new PeriodicTimer(interval);
        _flushLoop = RunFlushLoopAsync();
    }

    public void Append(string chunk)
    {
        lock (_gate)
        {
            _pending.Append(chunk);
        }
    }

    /// <summary>
    /// Dừng nhịp gom và phát nốt phần chữ còn lại. Gọi trước khi gửi BotStreamComplete để giữ đúng thứ tự sự kiện.
    /// </summary>
    public async Task CompleteAsync()
    {
        if (_isCompleted)
        {
            return;
        }
        _isCompleted = true;
        _timer.Dispose();
        await _flushLoop;
        await FlushAsync();
    }

    public async ValueTask DisposeAsync() => await CompleteAsync();

    private async Task RunFlushLoopAsync()
    {
        while (await _timer.WaitForNextTickAsync())
        {
            await FlushAsync();
        }
    }

    private async Task FlushAsync()
    {
        string batch;
        lock (_gate)
        {
            if (_pending.Length == 0)
            {
                return;
            }
            batch = _pending.ToString();
            _pending.Clear();
        }

        try
        {
            await _publish(batch);
        }
        catch (Exception ex)
        {
            // Mất 1 gói chữ trung gian không ảnh hưởng kết quả: BotStreamComplete luôn mang toàn văn câu trả lời
            _logger.LogWarning(ex, "Không phát được cụm chữ @bot qua SignalR.");
        }
    }
}
