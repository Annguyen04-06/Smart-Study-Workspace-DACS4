namespace SmartStudy.Domain.Exceptions;

/// <summary>
/// Ngoại lệ vi phạm quy tắc nghiệp vụ ở tầng Domain/Application (ánh xạ sang HTTP 400 Bad Request).
/// </summary>
public class DomainException : Exception
{
    public IReadOnlyList<string> Errors { get; }

    public DomainException(string message) : base(message)
    {
        Errors = Array.Empty<string>();
    }

    public DomainException(string message, IEnumerable<string> errors) : base(message)
    {
        Errors = errors.ToList().AsReadOnly();
    }
}
