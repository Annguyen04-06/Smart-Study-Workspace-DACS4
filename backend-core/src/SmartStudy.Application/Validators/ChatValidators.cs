using FluentValidation;
using SmartStudy.Application.DTOs.Chat;

namespace SmartStudy.Application.Validators;

/// <summary>
/// Bộ kiểm tra nội dung tin nhắn thảo luận trong phòng học.
/// </summary>
public class SendMessageRequestValidator : AbstractValidator<SendMessageRequestDto>
{
    public SendMessageRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Nội dung tin nhắn không được để trống.")
            .MaximumLength(4000).WithMessage("Tin nhắn tối đa 4000 ký tự.");
    }
}
