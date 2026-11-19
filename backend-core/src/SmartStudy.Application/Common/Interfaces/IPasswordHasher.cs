namespace SmartStudy.Application.Common.Interfaces;

/// <summary>
/// Hợp đồng băm và đối chiếu mật khẩu một chiều an toàn (BCrypt).
/// </summary>
public interface IPasswordHasher
{
    string HashPassword(string plainPassword);
    bool VerifyPassword(string plainPassword, string passwordHash);
}
