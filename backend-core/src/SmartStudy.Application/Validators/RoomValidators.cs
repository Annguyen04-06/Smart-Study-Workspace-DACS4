using FluentValidation;
using SmartStudy.Application.DTOs.Rooms;

namespace SmartStudy.Application.Validators;

/// <summary>
/// Bộ kiểm tra dữ liệu đầu vào khi tạo mới Phòng học và tham gia bằng mã mời InviteCode.
/// </summary>
public class CreateRoomRequestValidator : AbstractValidator<CreateRoomRequestDto>
{
    public CreateRoomRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên phòng học không được để trống.")
            .MinimumLength(3).WithMessage("Tên phòng học phải từ 3 ký tự trở lên.")
            .MaximumLength(150).WithMessage("Tên phòng học tối đa 150 ký tự.");

        RuleFor(x => x.SubjectCategory)
            .NotEmpty().WithMessage("Vui lòng chọn hoặc nhập chuyên ngành / môn học.")
            .MaximumLength(100).WithMessage("Tên chuyên ngành tối đa 100 ký tự.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Mô tả phòng học tối đa 500 ký tự.");
    }
}

public class JoinRoomByCodeRequestValidator : AbstractValidator<JoinRoomByCodeRequestDto>
{
    public JoinRoomByCodeRequestValidator()
    {
        RuleFor(x => x.InviteCode)
            .NotEmpty().WithMessage("Vui lòng nhập mã mời phòng học.")
            .MaximumLength(25).WithMessage("Mã mời tối đa 25 ký tự.");
    }
}
