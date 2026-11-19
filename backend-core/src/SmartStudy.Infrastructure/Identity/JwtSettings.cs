namespace SmartStudy.Infrastructure.Identity;

/// <summary>
/// Cấu hình phát hành và kiểm định chữ ký số JWT đọc từ appsettings.json.
/// </summary>
public class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "SmartStudyWorkspace.Backend";
    public string Audience { get; set; } = "SmartStudyWorkspace.Client";
    public int ExpiryMinutes { get; set; } = 1440;
}
