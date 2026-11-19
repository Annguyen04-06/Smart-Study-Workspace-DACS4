namespace SmartStudy.Domain.Exceptions;

/// <summary>
/// Ngoại lệ khi không tìm thấy tài nguyên yêu cầu trong cơ sở dữ liệu (ánh xạ sang HTTP 404 Not Found).
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }

    public NotFoundException(string resourceName, object key)
        : base($"Không tìm thấy {resourceName} với mã định danh ({key}).")
    {
    }
}
