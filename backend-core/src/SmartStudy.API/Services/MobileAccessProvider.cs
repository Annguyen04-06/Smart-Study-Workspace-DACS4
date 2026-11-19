using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace SmartStudy.API.Services;

/// <summary>
/// Thông tin mở App trên điện thoại: link HTTPS công khai (cài PWA 1 chạm, dùng Micro) và link mạng LAN.
/// </summary>
public sealed record MobileAccessInfo(
    string? PublicHttpsUrl,
    string? LanUrl,
    bool IsTunnelActive,
    DateTime? UpdatedAt
);

public sealed class MobileAccessOptions
{
    public const string SectionName = "MobileAccess";

    /// <summary>File do daemon-watchdog ghi link Cloudflare Tunnel (tương đối theo ContentRoot của API).</summary>
    public string TunnelUrlFile { get; set; } = "../../../scripts/local-vps/current-tunnel-url.txt";

    public int LanPort { get; set; } = 5500;

    /// <summary>Daemon xác minh link và ghi lại file mỗi ~60 giây; quá hạn này coi như đường hầm đã tắt.</summary>
    public int TunnelHeartbeatSeconds { get; set; } = 150;
}

/// <summary>
/// Đọc link HTTPS mới nhất từ current-tunnel-url.txt (daemon-watchdog chỉ ghi khi link đã kiểm chứng sống, ghi lại
/// định kỳ làm "nhịp tim") và tự dò IP mạng LAN của máy chủ (ưu tiên card mạng có Default Gateway, cache 30 giây).
/// </summary>
public sealed class MobileAccessProvider
{
    private static readonly TimeSpan LanIpCacheDuration = TimeSpan.FromSeconds(30);
    private static readonly string[] VirtualAdapterMarkers = ["vEthernet", "VirtualBox", "VMware", "Hyper-V", "WSL", "Docker", "Radmin", "Loopback"];

    private readonly MobileAccessOptions _options;
    private readonly string _tunnelUrlFile;
    private readonly object _lanLock = new();
    private string? _cachedLanIp;
    private DateTime _lanIpCachedAtUtc = DateTime.MinValue;

    public MobileAccessProvider(IOptions<MobileAccessOptions> options, IHostEnvironment environment)
    {
        _options = options.Value;
        _tunnelUrlFile = Path.GetFullPath(_options.TunnelUrlFile, environment.ContentRootPath);
    }

    public MobileAccessInfo GetMobileAccess(bool includeLanUrl)
    {
        var (tunnelUrl, updatedAtUtc) = ReadTunnelUrl();
        var isTunnelActive = tunnelUrl is not null
            && updatedAtUtc is not null
            && (DateTime.UtcNow - updatedAtUtc.Value).TotalSeconds <= _options.TunnelHeartbeatSeconds;

        var lanIp = includeLanUrl ? GetLanIpAddress() : null;
        return new MobileAccessInfo(
            PublicHttpsUrl: isTunnelActive ? tunnelUrl : null,
            LanUrl: lanIp is null ? null : $"http://{lanIp}:{_options.LanPort}",
            IsTunnelActive: isTunnelActive,
            UpdatedAt: updatedAtUtc);
    }

    /// <summary>
    /// Chỉ trả IP nội bộ cho thiết bị trong mạng LAN/máy chủ; người truy cập qua Internet (Cloudflare) không thấy IP nội bộ.
    /// </summary>
    public static bool IsLocalNetworkAddress(IPAddress? address)
    {
        if (address is null)
        {
            return false;
        }
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal || address.IsIPv6UniqueLocal;
        }

        var bytes = address.GetAddressBytes();
        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            || (bytes[0] == 192 && bytes[1] == 168)
            || (bytes[0] == 169 && bytes[1] == 254);
    }

    private (string? Url, DateTime? UpdatedAtUtc) ReadTunnelUrl()
    {
        if (!File.Exists(_tunnelUrlFile))
        {
            return (null, null);
        }

        try
        {
            // Daemon có thể đang ghi file cùng lúc => mở chế độ chia sẻ đọc/ghi
            using var stream = new FileStream(_tunnelUrlFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            var firstLine = reader.ReadLine()?.Trim();
            var isValidHttpsUrl = Uri.TryCreate(firstLine, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
            return (isValidHttpsUrl ? uri!.GetLeftPart(UriPartial.Authority) : null, File.GetLastWriteTimeUtc(_tunnelUrlFile));
        }
        catch (IOException)
        {
            return (null, null);
        }
    }

    private string? GetLanIpAddress()
    {
        lock (_lanLock)
        {
            if (DateTime.UtcNow - _lanIpCachedAtUtc < LanIpCacheDuration)
            {
                return _cachedLanIp;
            }

            _cachedLanIp = DetectLanIpAddress();
            _lanIpCachedAtUtc = DateTime.UtcNow;
            return _cachedLanIp;
        }
    }

    private static string? DetectLanIpAddress()
    {
        var candidates = NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up
                && nic.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .Select(nic =>
            {
                var properties = nic.GetIPProperties();
                var ipv4 = properties.UnicastAddresses
                    .Select(unicast => unicast.Address)
                    .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork
                        && !IPAddress.IsLoopback(ip)
                        && !ip.ToString().StartsWith("169.254.", StringComparison.Ordinal));
                var hasDefaultGateway = properties.GatewayAddresses.Any(gateway =>
                    gateway.Address.AddressFamily == AddressFamily.InterNetwork && !gateway.Address.Equals(IPAddress.Any));
                var isVirtual = VirtualAdapterMarkers.Any(marker =>
                    nic.Name.Contains(marker, StringComparison.OrdinalIgnoreCase)
                    || nic.Description.Contains(marker, StringComparison.OrdinalIgnoreCase));
                return (Address: ipv4, HasDefaultGateway: hasDefaultGateway, IsVirtual: isVirtual);
            })
            .Where(candidate => candidate.Address is not null)
            .OrderByDescending(candidate => candidate.HasDefaultGateway)
            .ThenBy(candidate => candidate.IsVirtual)
            .ToList();

        return candidates.FirstOrDefault().Address?.ToString();
    }
}
