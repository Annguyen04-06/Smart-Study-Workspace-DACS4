using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SmartStudy.API.Services;

/// <summary>
/// Thông tin phiên bản build hiện tại phục vụ cơ chế đồng bộ giao diện OTA Auto-Sync cho PWA.
/// </summary>
public sealed record AppBuildVersionInfo(
    string Version,
    DateTime UpdatedAt,
    string ReleaseNotes
);

/// <summary>
/// Dịch vụ tự động tính mã buildVersion duy nhất dựa trên thời điểm biên dịch Assembly
/// kết hợp hash nội dung/metadata toàn bộ thư mục /frontend (cache TTL 5 giây để vừa cực nhanh
/// vừa phản ánh tức thì khi server build mới hoặc cập nhật tệp Frontend).
/// </summary>
public sealed class AppVersionProvider
{
    private static readonly string[] TrackedExtensions =
    [
        ".html", ".js", ".mjs", ".css", ".webmanifest", ".json"
    ];

    private readonly string? _frontendRoot;
    private readonly string _assemblyStamp;
    private readonly DateTime _assemblyBuildUtc;
    private readonly object _syncLock = new();

    private AppBuildVersionInfo? _cachedInfo;
    private DateTime _lastComputedUtc = DateTime.MinValue;

    public AppVersionProvider(string? frontendRoot)
    {
        _frontendRoot = frontendRoot;
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        _assemblyBuildUtc = !string.IsNullOrWhiteSpace(assemblyLocation) && File.Exists(assemblyLocation)
            ? File.GetLastWriteTimeUtc(assemblyLocation)
            : DateTime.UtcNow;
        _assemblyStamp = _assemblyBuildUtc.ToString("yyyyMMddHHmmss");
    }

    public AppBuildVersionInfo GetCurrentVersion()
    {
        lock (_syncLock)
        {
            if (_cachedInfo is not null && (DateTime.UtcNow - _lastComputedUtc).TotalSeconds < 5)
            {
                return _cachedInfo;
            }

            _cachedInfo = ComputeVersionSnapshot();
            _lastComputedUtc = DateTime.UtcNow;
            return _cachedInfo;
        }
    }

    private AppBuildVersionInfo ComputeVersionSnapshot()
    {
        var latestWriteUtc = _assemblyBuildUtc;
        var releaseNotes = "OTA Auto-Sync PWA + Offline-First IndexedDB + AI Weakness Radar & Scratchpad.";

        using var sha = SHA256.Create();
        var signatureBuilder = new StringBuilder();
        signatureBuilder.Append("asm:").Append(_assemblyStamp).Append('|');

        if (!string.IsNullOrWhiteSpace(_frontendRoot) && Directory.Exists(_frontendRoot))
        {
            var versionJsonPath = Path.Combine(_frontendRoot, "version.json");
            if (File.Exists(versionJsonPath))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(versionJsonPath));
                    if (doc.RootElement.TryGetProperty("releaseNotes", out var notesProp) &&
                        !string.IsNullOrWhiteSpace(notesProp.GetString()))
                    {
                        releaseNotes = notesProp.GetString()!;
                    }
                    if (doc.RootElement.TryGetProperty("version", out var verProp) &&
                        !string.IsNullOrWhiteSpace(verProp.GetString()))
                    {
                        signatureBuilder.Append("manifestVer:").Append(verProp.GetString()).Append('|');
                    }
                }
                catch
                {
                    // Bỏ qua nếu version.json đang được ghi dở
                }
            }

            var frontendFiles = Directory
                .EnumerateFiles(_frontendRoot, "*.*", SearchOption.AllDirectories)
                .Where(path =>
                {
                    var ext = Path.GetExtension(path);
                    return TrackedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
                })
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

            foreach (var filePath in frontendFiles)
            {
                var info = new FileInfo(filePath);
                if (!info.Exists)
                {
                    continue;
                }

                var relPath = Path.GetRelativePath(_frontendRoot, filePath).Replace('\\', '/');
                var writeUtc = info.LastWriteTimeUtc;
                if (writeUtc > latestWriteUtc)
                {
                    latestWriteUtc = writeUtc;
                }

                signatureBuilder
                    .Append(relPath)
                    .Append(':')
                    .Append(info.Length)
                    .Append(':')
                    .Append(writeUtc.Ticks)
                    .Append(';');
            }
        }

        var hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(signatureBuilder.ToString()));
        var shortHash = Convert.ToHexString(hashBytes)[..10].ToLowerInvariant();
        var versionCode = $"{latestWriteUtc:yyyy.MM.dd.HHmm}-{shortHash}";

        return new AppBuildVersionInfo(
            Version: versionCode,
            UpdatedAt: latestWriteUtc,
            ReleaseNotes: releaseNotes);
    }
}
