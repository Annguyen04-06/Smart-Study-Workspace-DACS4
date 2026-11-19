using FluentValidation;
using SmartStudy.Application.DTOs.Flashcards;

namespace SmartStudy.Application.Validators;

/// <summary>
/// Bộ kiểm tra dữ liệu cho luồng sinh bộ thẻ Flashcard và cập nhật mức độ thuộc bài.
/// </summary>
public class GenerateFlashcardDeckRequestValidator : AbstractValidator<GenerateFlashcardDeckRequestDto>
{
    public GenerateFlashcardDeckRequestValidator()
    {
        RuleFor(x => x.MaterialId)
            .GreaterThan(0).WithMessage("Vui lòng chọn tài liệu PDF nguồn hợp lệ.");

        RuleFor(x => x.CardCount)
            .InclusiveBetween(3, 40).WithMessage("Số lượng thẻ Flashcard phải từ 3 đến 40 thẻ.");

        RuleFor(x => x.Title)
            .MaximumLength(200).WithMessage("Tiêu đề bộ thẻ tối đa 200 ký tự.");
    }
}

public class UpdateFlashcardMasteryRequestValidator : AbstractValidator<UpdateFlashcardMasteryRequestDto>
{
    public UpdateFlashcardMasteryRequestValidator()
    {
        RuleFor(x => x.MasteryLevel)
            .InclusiveBetween(0, 2)
            .WithMessage("Mức độ ghi nhớ chỉ nhận giá trị 0 (Chưa thuộc), 1 (Tạm nhớ) hoặc 2 (Đã thuộc).");
    }
}
