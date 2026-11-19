using FluentValidation;
using SmartStudy.Application.DTOs.Quizzes;

namespace SmartStudy.Application.Validators;

/// <summary>
/// Bộ kiểm tra tham số sinh đề trắc nghiệm AI và nộp bài thi.
/// </summary>
public class GenerateQuizRequestValidator : AbstractValidator<GenerateQuizRequestDto>
{
    private static readonly string[] AllowedDifficulties = { "Easy", "Medium", "Hard" };

    public GenerateQuizRequestValidator()
    {
        RuleFor(x => x.MaterialId)
            .GreaterThan(0).WithMessage("Vui lòng chọn tài liệu PDF nguồn hợp lệ.");

        RuleFor(x => x.QuestionCount)
            .InclusiveBetween(3, 30).WithMessage("Số câu hỏi trắc nghiệm phải nằm trong khoảng từ 3 đến 30 câu.");

        RuleFor(x => x.DurationMinutes)
            .InclusiveBetween(3, 120).WithMessage("Thời gian làm bài phải từ 3 đến 120 phút.");

        RuleFor(x => x.Difficulty)
            .Must(d => AllowedDifficulties.Contains(d, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Độ khó chỉ chấp nhận Easy, Medium hoặc Hard.");

        RuleFor(x => x.Title)
            .MaximumLength(200).WithMessage("Tiêu đề đề thi tối đa 200 ký tự.");
    }
}

public class SubmitQuizRequestValidator : AbstractValidator<SubmitQuizRequestDto>
{
    public SubmitQuizRequestValidator()
    {
        RuleFor(x => x.TimeSpentSeconds)
            .GreaterThanOrEqualTo(0).WithMessage("Thời gian làm bài không hợp lệ.");

        RuleFor(x => x.Answers)
            .NotNull().WithMessage("Danh sách câu trả lời không hợp lệ.");
    }
}
