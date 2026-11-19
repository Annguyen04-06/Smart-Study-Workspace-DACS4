using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartStudy.Application.Common.Interfaces;
using SmartStudy.Domain.Exceptions;

namespace SmartStudy.Infrastructure.ExternalServices;

/// <summary>
/// Cấu hình máy chủ thư điện tử Google Gmail SMTP (sử dụng Google App Password 16 ký tự).
/// </summary>
public class GoogleSmtpSettings
{
    public const string SectionName = "GoogleSmtp";

    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string SenderName { get; set; } = "Smart Study Workspace VKU";
    public string SenderEmail { get; set; } = string.Empty;
    public string AppPassword { get; set; } = string.Empty;
}

/// <summary>
/// Dịch vụ gửi Email mã OTP đặt lại mật khẩu thật qua Google Gmail SMTP (smtp.gmail.com:587).
/// </summary>
public class GoogleSmtpEmailSender : IEmailSender
{
    private readonly GoogleSmtpSettings _settings;
    private readonly ILogger<GoogleSmtpEmailSender> _logger;

    public GoogleSmtpEmailSender(
        IOptionsSnapshot<GoogleSmtpSettings> options,
        ILogger<GoogleSmtpEmailSender> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<EmailDeliveryResult> SendPasswordResetOtpAsync(
        string recipientEmail,
        string recipientName,
        string otpCode,
        CancellationToken cancellationToken = default)
    {
        var senderEmail = !string.IsNullOrWhiteSpace(_settings.SenderEmail)
            ? _settings.SenderEmail.Trim()
            : Environment.GetEnvironmentVariable("GOOGLE_SMTP_EMAIL")?.Trim() ?? string.Empty;

        var rawAppPassword = !string.IsNullOrWhiteSpace(_settings.AppPassword)
            ? _settings.AppPassword
            : Environment.GetEnvironmentVariable("GOOGLE_SMTP_APP_PASSWORD") ?? string.Empty;

        var normalizedAppPassword = new string(rawAppPassword.Where(c => !char.IsWhiteSpace(c)).ToArray());

        if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(normalizedAppPassword))
        {
            throw new DomainException("Dịch vụ gửi thư khôi phục mật khẩu hiện chưa sẵn sàng. Vui lòng thử lại sau.");
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(senderEmail, _settings.SenderName, Encoding.UTF8),
                Subject = $"[{otpCode}] Mã xác thực đặt lại mật khẩu — Smart Study Workspace",
                Body = BuildHtmlEmailBody(recipientName, otpCode),
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8
            };
            message.To.Add(new MailAddress(recipientEmail.Trim(), recipientName, Encoding.UTF8));

            using var smtpClient = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl,
                TargetName = $"STARTTLS/{_settings.Host}",
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(senderEmail, normalizedAppPassword),
                Timeout = 15000
            };

            await smtpClient.SendMailAsync(message, cancellationToken);
            _logger.LogInformation("[SmartStudy OTP] Đã gửi thành công email OTP từ {Sender} tới {Recipient}.", senderEmail, recipientEmail);

            return new EmailDeliveryResult(
                SentViaSmtp: true,
                DeliveryNote: "Mã xác thực 6 chữ số đã được gửi tới hộp thư Email của bạn.");
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex, "[SmartStudy OTP] Lỗi xác thực hoặc gửi thư Google SMTP với SenderEmail={Sender}.", senderEmail);
            throw new DomainException("Không thể gửi thư xác thực qua máy chủ Gmail (khóa App Password chưa khớp với tài khoản gửi hoặc đã hết hiệu lực).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SmartStudy OTP] Lỗi kết nối máy chủ thư điện tử khi gửi tới {Recipient}.", recipientEmail);
            throw new DomainException("Không thể kết nối máy chủ gửi thư. Vui lòng thử lại sau giây lát.");
        }
    }

    private static string BuildHtmlEmailBody(string fullName, string otpCode)
    {
        var safeName = WebUtility.HtmlEncode(fullName);
        return $@"
<!DOCTYPE html>
<html lang=""vi"">
<body style=""margin:0;padding:24px;background-color:#f8fafc;font-family:'Segoe UI',Inter,Arial,sans-serif;color:#0f172a;"">
  <div style=""max-width:520px;margin:0 auto;background:#ffffff;border:1px solid #e2e8f0;border-radius:20px;overflow:hidden;box-shadow:0 10px 30px rgba(15,23,42,0.06);"">
    <div style=""background:linear-gradient(135deg,#4f46e5,#7c3aed);padding:24px 28px;color:#ffffff;"">
      <div style=""font-size:12px;font-weight:700;letter-spacing:0.08em;text-transform:uppercase;opacity:0.9;"">Smart Study Workspace VKU</div>
      <h1 style=""margin:8px 0 0;font-size:20px;font-weight:800;"">Yêu cầu Đặt lại Mật khẩu</h1>
    </div>
    <div style=""padding:28px;"">
      <p style=""margin:0 0 12px;font-size:14px;line-height:1.6;"">Xin chào <strong>{safeName}</strong>,</p>
      <p style=""margin:0 0 20px;font-size:14px;line-height:1.6;color:#475569;"">
        Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản học tập của bạn. Vui lòng nhập mã xác thực 6 chữ số dưới đây để tiếp tục:
      </p>
      <div style=""text-align:center;margin:24px 0;padding:18px;background:#eef2ff;border:1px dashed #6366f1;border-radius:16px;"">
        <span style=""font-family:'Courier New',monospace;font-size:32px;font-weight:800;letter-spacing:8px;color:#4338ca;"">{otpCode}</span>
      </div>
      <p style=""margin:0 0 8px;font-size:13px;color:#64748b;"">
        &bull; Mã xác thực có hiệu lực trong vòng <strong>10 phút</strong>.<br/>
        &bull; Tuyệt đối không chia sẻ mã này cho bất kỳ ai khác.
      </p>
    </div>
    <div style=""padding:16px 28px;background:#f8fafc;border-top:1px solid #f1f5f9;font-size:12px;color:#94a3b8;text-align:center;"">
      &copy; 2026 Smart Study Workspace &mdash; Nền tảng Học tập Hợp tác Thông minh
    </div>
  </div>
</body>
</html>";
    }
}
