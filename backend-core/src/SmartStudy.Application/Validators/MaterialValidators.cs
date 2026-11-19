using FluentValidation;
using SmartStudy.Application.DTOs.Materials;

namespace SmartStudy.Application.Validators;

/// <summary>
/// Bộ kiểm tra siêu dữ liệu khi sinh viên tải lên học liệu PDF vào phòng học.
/// </summary>
public class UploadMaterialMetadataValidator : AbstractValidator<UploadMaterialMetadataDto>
{
    public UploadMaterialMetadataValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tiêu đề học liệu không được để trống.")
            .MaximumLength(200).WithMessage("Tiêu đề học liệu tối đa 200 ký tự.");
    }
}
