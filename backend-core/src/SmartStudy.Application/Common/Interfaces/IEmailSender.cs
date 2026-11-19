namespace SmartStudy.Application.Common.Interfaces;

/// <summary>
/// Kết quả chuyển phát thư điện tử khôi phục mật khẩu (qua Google Gmail SMTP hoặc chế độ thử nghiệm cục bộ).
/// </summary>
public sealed record EmailDeliveryResult(bool SentViaSmtp, string DeliveryNote);

/// <summary>
/// Hợp đồng dịch vụ gửi Email mã xác thực OTP khôi phục mật khẩu.
/// </summary>
public interface IEmailSender
{
    Task<EmailDeliveryResult> SendPasswordResetOtpAsync(
        string recipientEmail,
        string recipientName,
        string otpCode,
        CancellationToken cancellationToken = default);
}
