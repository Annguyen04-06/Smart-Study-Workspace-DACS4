using FluentValidation;
using SmartStudy.Application.DTOs.Admin;

namespace SmartStudy.Application.Validators;

/// <summary>
/// Bộ kiểm tra trạng thái phòng học khi Quản trị viên cập nhật.
/// </summary>
public class UpdateRoomStatusRequestValidator : AbstractValidator<UpdateRoomStatusRequestDto>
{
    private static readonly string[] ValidStatuses = { "Pending", "Active", "Closed" };

    public UpdateRoomStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => ValidStatuses.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Trạng thái phòng học phải là Pending, Active hoặc Closed.");
    }
}
