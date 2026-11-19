using SmartStudy.Application.Common.Interfaces;

namespace SmartStudy.Infrastructure.Storage;

/// <summary>
/// Cấu hình thư mục gốc lưu trữ tệp vật lý (mặc định trỏ vào wwwroot của SmartStudy.API).
/// </summary>
public class FileStorageOptions
{
    public string WebRootPath { get; set; } = string.Empty;
}

/// <summary>
/// Lưu trữ an toàn tệp học liệu PDF và ảnh đại diện vào wwwroot/uploads (chống Path Traversal).
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly string _webRootPath;

    public LocalFileStorageService(FileStorageOptions options)
    {
        _webRootPath = string.IsNullOrWhiteSpace(options.WebRootPath)
            ? Path.Combine(AppContext.BaseDirectory, "wwwroot")
            : options.WebRootPath;
    }

    public async Task<string> SaveMaterialPdfAsync(
        Stream fileStream,
        string originalFileName,
        CancellationToken cancellationToken = default)
    {
        return await SaveSafeFileAsync(fileStream, "materials", ".pdf", cancellationToken);
    }

    public async Task<string> SaveAvatarImageAsync(
        Stream fileStream,
        string originalFileName,
        CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = ".png";
        }
        return await SaveSafeFileAsync(fileStream, "avatars", ext, cancellationToken);
    }

    public Task DeleteFileIfExistsAsync(string? storedRelativePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storedRelativePath))
        {
            return Task.CompletedTask;
        }

        // Không xóa các tệp PDF mẫu được khởi tạo từ DbSeeder
        if (storedRelativePath.Contains("seed-", StringComparison.OrdinalIgnoreCase) ||
            storedRelativePath.Contains("vku-", StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        var normalizedRelative = storedRelativePath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
        var fullUploadsRoot = Path.GetFullPath(Path.Combine(_webRootPath, "uploads"));
        var fullTarget = Path.GetFullPath(Path.Combine(_webRootPath, normalizedRelative));

        if (fullTarget.StartsWith(fullUploadsRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(fullTarget))
        {
            File.Delete(fullTarget);
        }

        return Task.CompletedTask;
    }

    private async Task<string> SaveSafeFileAsync(
        Stream sourceStream,
        string subFolder,
        string safeExtension,
        CancellationToken cancellationToken)
    {
        var targetDirectory = Path.Combine(_webRootPath, "uploads", subFolder);
        Directory.CreateDirectory(targetDirectory);

        var uniqueFileName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{safeExtension}";
        var physicalPath = Path.Combine(targetDirectory, uniqueFileName);

        await using var output = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await sourceStream.CopyToAsync(output, cancellationToken);

        return $"/uploads/{subFolder}/{uniqueFileName}";
    }
}
