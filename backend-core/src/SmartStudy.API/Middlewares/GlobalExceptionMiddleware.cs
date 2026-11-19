using System.Net;
using System.Text.Json;
using SmartStudy.Application.Common.Models;
using SmartStudy.Domain.Exceptions;

namespace SmartStudy.API.Middlewares;

/// <summary>
/// Middleware bắt tập trung mọi ngoại lệ trên pipeline HTTP và chuẩn hóa thành phản hồi JSON ApiResponse camelCase.
/// </summary>
public class GlobalExceptionMiddleware
{
    private static readonly JsonSerializerOptions CamelCaseJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message, errors) = exception switch
        {
            DomainException domainEx => (
                HttpStatusCode.BadRequest,
                domainEx.Message,
                domainEx.Errors.Count > 0 ? domainEx.Errors : new[] { domainEx.Message }),

            UnauthorizedAccessException unauthorizedEx => (
                HttpStatusCode.Unauthorized,
                string.IsNullOrWhiteSpace(unauthorizedEx.Message) ? "Vui lòng đăng nhập để tiếp tục." : unauthorizedEx.Message,
                Array.Empty<string>()),

            ForbiddenAccessException forbiddenEx => (
                HttpStatusCode.Forbidden,
                forbiddenEx.Message,
                new[] { forbiddenEx.Message }),

            NotFoundException notFoundEx => (
                HttpStatusCode.NotFound,
                notFoundEx.Message,
                new[] { notFoundEx.Message }),

            _ => (
                HttpStatusCode.InternalServerError,
                "Đã xảy ra lỗi hệ thống ngoài ý muốn. Vui lòng thử lại sau.",
                new[] { exception.Message })
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Lỗi nội bộ chưa xử lý tại đường dẫn {Path}", context.Request.Path);
        }

        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.StatusCode = (int)statusCode;

        var responsePayload = ApiResponse<object?>.Fail(message, errors);
        var json = JsonSerializer.Serialize(responsePayload, CamelCaseJson);
        await context.Response.WriteAsync(json);
    }
}
