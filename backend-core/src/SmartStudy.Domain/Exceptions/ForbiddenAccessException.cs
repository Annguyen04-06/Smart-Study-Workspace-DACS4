namespace SmartStudy.Domain.Exceptions;

/// <summary>
/// Ngoại lệ khi người dùng không đủ thẩm quyền truy cập tài nguyên (ánh xạ sang HTTP 403 Forbidden).
/// </summary>
public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string message = "Bạn không có quyền thực hiện thao tác này.")
        : base(message)
    {
    }
}
