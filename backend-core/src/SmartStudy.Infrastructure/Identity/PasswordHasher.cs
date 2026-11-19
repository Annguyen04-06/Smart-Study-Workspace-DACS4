using SmartStudy.Application.Common.Interfaces;

namespace SmartStudy.Infrastructure.Identity;

/// <summary>
/// Băm và xác thực mật khẩu một chiều bằng thuật toán BCrypt (workFactor = 11).
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 11;

    public string HashPassword(string plainPassword)
    {
        return BCrypt.Net.BCrypt.HashPassword(plainPassword, WorkFactor);
    }

    public bool VerifyPassword(string plainPassword, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(plainPassword) || string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(plainPassword, passwordHash);
        }
        catch
        {
            return false;
        }
    }
}
