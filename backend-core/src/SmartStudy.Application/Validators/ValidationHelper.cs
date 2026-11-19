using FluentValidation;
using SmartStudy.Domain.Exceptions;

namespace SmartStudy.Application.Validators;

/// <summary>
/// Tiện ích thực thi FluentValidation tại tầng Application và chuyển lỗi sang DomainException.
/// </summary>
public static class ValidationHelper
{
    public static async Task ValidateOrThrowAsync<T>(
        IValidator<T> validator,
        T instance,
        CancellationToken cancellationToken = default)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken);
        if (!result.IsValid)
        {
            var errorMessages = result.Errors.Select(e => e.ErrorMessage).Distinct().ToList();
            throw new DomainException(errorMessages.First(), errorMessages);
        }
    }
}
