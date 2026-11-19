namespace SmartStudy.Domain.Common;

/// <summary>
/// Lớp thực thể cơ sở tổng quát định nghĩa khóa chính cho các Entity trong tầng Domain.
/// Hoàn toàn độc lập, không phụ thuộc bất kỳ thư viện hạ tầng nào.
/// </summary>
public abstract class BaseEntity<TKey>
{
    public TKey Id { get; set; } = default!;
}

/// <summary>
/// Lớp thực thể cơ sở mặc định với khóa chính kiểu INT tự tăng và mốc thời gian tạo UTC.
/// </summary>
public abstract class BaseEntity : BaseEntity<int>
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
