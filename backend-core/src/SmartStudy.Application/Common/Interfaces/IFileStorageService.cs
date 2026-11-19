namespace SmartStudy.Application.Common.Interfaces;

/// <summary>
/// Hợp đồng lưu trữ và xóa an toàn tệp học liệu PDF cũng như ảnh đại diện Avatar.
/// </summary>
public interface IFileStorageService
{
    Task<string> SaveMaterialPdfAsync(Stream fileStream, string originalFileName, CancellationToken cancellationToken = default);
    Task<string> SaveAvatarImageAsync(Stream fileStream, string originalFileName, CancellationToken cancellationToken = default);
    Task DeleteFileIfExistsAsync(string? storedRelativePath, CancellationToken cancellationToken = default);
}
