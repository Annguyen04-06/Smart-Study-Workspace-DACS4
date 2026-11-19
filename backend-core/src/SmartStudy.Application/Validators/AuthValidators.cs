using FluentValidation;
using SmartStudy.Application.DTOs.Auth;

namespace SmartStudy.Application.Validators;

/// <summary>
/// Bộ kiểm tra dữ liệu đầu vào cho Đăng ký, Đăng nhập, Cập nhật hồ sơ và Đổi mật khẩu.
/// </summary>
public class RegisterRequestValidator : AbstractValidator<RegisterRequestDto>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.StudentCode)
            .NotEmpty().WithMessage("Mã số sinh viên không được để trống.")
            .MaximumLength(20).WithMessage("Mã số sinh viên tối đa 20 ký tự.")
            .Matches("^[A-Za-z0-9_-]+$").WithMessage("Mã số sinh viên chỉ gồm chữ cái, chữ số, gạch ngang hoặc gạch dưới.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ và tên không được để trống.")
            .MinimumLength(2).WithMessage("Họ và tên phải từ 2 ký tự trở lên.")
            .MaximumLength(100).WithMessage("Họ và tên tối đa 100 ký tự.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Định dạng Email không hợp lệ.")
            .MaximumLength(150).WithMessage("Email tối đa 150 ký tự.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .MinimumLength(6).WithMessage("Mật khẩu phải có ít nhất 6 ký tự.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Mật khẩu xác nhận không khớp.");
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequestDto>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Vui lòng nhập Email đăng nhập.")
            .EmailAddress().WithMessage("Định dạng Email không hợp lệ.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Vui lòng nhập mật khẩu.");
    }
}

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequestDto>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ và tên không được để trống.")
            .MaximumLength(100).WithMessage("Họ và tên tối đa 100 ký tự.");

        RuleFor(x => x.StudentCode)
            .MaximumLength(20).WithMessage("Mã số sinh viên tối đa 20 ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.StudentCode));
    }
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequestDto>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Vui lòng nhập mật khẩu hiện tại.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Vui lòng nhập mật khẩu mới.")
            .MinimumLength(6).WithMessage("Mật khẩu mới phải có ít nhất 6 ký tự.");

        RuleFor(x => x.ConfirmNewPassword)
            .Equal(x => x.NewPassword).WithMessage("Xác nhận mật khẩu mới không khớp.");
    }
}

public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequestDto>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Vui lòng nhập địa chỉ Email đã đăng ký.")
            .EmailAddress().WithMessage("Định dạng địa chỉ Email không hợp lệ.");
    }
}

public class ResetPasswordWithOtpRequestValidator : AbstractValidator<ResetPasswordWithOtpRequestDto>
{
    public ResetPasswordWithOtpRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Định dạng Email không hợp lệ.");

        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage("Vui lòng nhập mã xác thực 6 chữ số.")
            .Matches("^[0-9]{6}$").WithMessage("Mã xác thực phải gồm đúng 6 chữ số.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Vui lòng nhập mật khẩu mới.")
            .MinimumLength(6).WithMessage("Mật khẩu mới phải có ít nhất 6 ký tự.");

        RuleFor(x => x.ConfirmNewPassword)
            .Equal(x => x.NewPassword).WithMessage("Xác nhận mật khẩu mới không khớp.");
    }
}
