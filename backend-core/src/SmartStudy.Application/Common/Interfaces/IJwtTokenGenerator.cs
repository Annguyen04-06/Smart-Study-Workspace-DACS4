using SmartStudy.Domain.Entities;

namespace SmartStudy.Application.Common.Interfaces;

/// <summary>
/// Hợp đồng sinh chuỗi JWT Bearer Token chứa Claims định danh và vai trò người dùng.
/// </summary>
public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAt) GenerateToken(User user);
}
