namespace SmartStudy.Application.Common.Models;

/// <summary>
/// Khung dữ liệu JSON đồng nhất bắt buộc cho toàn bộ REST API của hệ thống:
/// { "success": true, "message": "...", "data": ..., "errors": [] }
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();

    public static ApiResponse<T> Ok(T data, string message = "Thao tác thành công.")
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data,
            Errors = Array.Empty<string>()
        };
    }

    public static ApiResponse<T> Fail(string message, IEnumerable<string>? errors = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message,
            Data = default,
            Errors = errors?.ToList().AsReadOnly() ?? (IReadOnlyList<string>)Array.Empty<string>()
        };
    }
}

/// <summary>
/// Lớp tiện ích không tham số kiểu cho các API không trả về dữ liệu chi tiết.
/// </summary>
public class ApiResponse : ApiResponse<object?>
{
    public static ApiResponse OkMessage(string message = "Thao tác thành công.")
    {
        return new ApiResponse
        {
            Success = true,
            Message = message,
            Data = null,
            Errors = Array.Empty<string>()
        };
    }
}
