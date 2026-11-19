<#
==============================================================================
 SMART STUDY WORKSPACE - THƯ VIỆN DÙNG CHUNG CHO LOCAL VPS
 Được dot-source bởi daemon-watchdog.ps1 (chạy ngầm Backend + AI + Cloudflare Tunnel) và run-local-vps.ps1
 (hiển thị link HTTPS + QR Portal). Gồm: đường dẫn chuẩn, quản lý tiến trình (nhận diện PID + thời điểm khởi chạy),
 giải phóng cổng, kiểm tra sức khỏe HTTP, tải/kiểm chứng Cloudflare Tunnel, đọc log đang ghi dở và dò IP mạng LAN.
==============================================================================
#>

# ------------------------------------------------------------------------------
# ĐƯỜNG DẪN & CẤU HÌNH CHUẨN
# ------------------------------------------------------------------------------
$VpsRoot           = Split-Path -Parent $PSScriptRoot
$ProjectRoot       = (Resolve-Path (Join-Path $VpsRoot "..\..")).Path
$LogDir            = Join-Path $VpsRoot "logs"
$BinDir            = Join-Path $VpsRoot "bin"
$PublishDir        = Join-Path $VpsRoot "publish-backend"
$BackendProjectDir = Join-Path $ProjectRoot "backend-core\src\SmartStudy.API"
$BackendCsproj     = Join-Path $BackendProjectDir "SmartStudy.API.csproj"
$BackendExe        = Join-Path $PublishDir "SmartStudy.API.exe"
$BackendDll        = Join-Path $PublishDir "SmartStudy.API.dll"
$AiDir             = Join-Path $ProjectRoot "ai-service"
$Cloudflared       = Join-Path $BinDir "cloudflared.exe"

$BackendPorts      = @(5000, 5500)
$BackendUrls       = "http://0.0.0.0:5000;http://0.0.0.0:5500"
$AiPort            = 8000
$BackendHealthUrl  = "http://127.0.0.1:5000/api/health"
$AiHealthUrl       = "http://127.0.0.1:$AiPort/api/v1/health"

$DaemonStateFile   = Join-Path $LogDir "daemon.state.json"
$TunnelStateFile   = Join-Path $LogDir "tunnel.state.json"

# Cloudflare HTTPS Tunnel do daemon quản lý: link đã kiểm chứng được ghi vào current-tunnel-url.txt
# (Backend đọc file này cho GET /api/system/mobile-access). Có file tunnel.disabled => tắt đường hầm Internet.
$TunnelUrlFile      = Join-Path $VpsRoot "current-tunnel-url.txt"
$TunnelDisabledFlag = Join-Path $VpsRoot "tunnel.disabled"
# Trỏ vào 127.0.0.1 (IPv4) vì Kestrel lắng nghe 0.0.0.0 — "localhost" có thể phân giải sang ::1 (IPv6)
$TunnelTargetUrl    = "http://127.0.0.1:5500"
$SilentLauncher    = Join-Path $VpsRoot "start-silent-background.vbs"
$StartupDir        = [Environment]::GetFolderPath("Startup")
$AutostartFile     = Join-Path $StartupDir "SmartStudyAutoStart.vbs"
$LegacyAutostart   = Join-Path $StartupDir "SmartStudyLocalVPS.cmd"

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

# ------------------------------------------------------------------------------
# GHI FILE / ĐỌC LOG
# ------------------------------------------------------------------------------
function Write-Utf8File([string]$Path, [string]$Content) {
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

# Đọc file log đang bị tiến trình khác ghi (FileShare.ReadWrite) mà không gây lỗi khóa file
function Read-SharedFile([string]$Path) {
    if (-not (Test-Path $Path)) { return "" }
    try {
        $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
        try {
            $reader = New-Object System.IO.StreamReader($stream)
            return $reader.ReadToEnd()
        } finally {
            $stream.Dispose()
        }
    } catch {
        return ""
    }
}

function Get-LogTail([string]$Path, [int]$Lines = 15) {
    $content = Read-SharedFile $Path
    if (-not $content) { return "" }
    return (($content -split "`r?`n") | Select-Object -Last $Lines) -join [Environment]::NewLine
}

# Xoay vòng log quá lớn (giữ 1 bản .old) để chạy 24/7 không làm đầy ổ đĩa
function Invoke-LogRotation([string]$Path, [long]$MaxBytes = 5MB) {
    if ((Test-Path $Path) -and ((Get-Item $Path).Length -gt $MaxBytes)) {
        Move-Item -Path $Path -Destination "$Path.old" -Force -ErrorAction SilentlyContinue
    }
}

function Read-JsonState([string]$Path) {
    if (-not (Test-Path $Path)) { return $null }
    try {
        return (Get-Content -Path $Path -Raw -Encoding UTF8 | ConvertFrom-Json)
    } catch {
        return $null
    }
}

function Write-JsonState([string]$Path, [hashtable]$State) {
    $State.updatedAt = (Get-Date).ToString("o")
    Write-Utf8File $Path ($State | ConvertTo-Json -Depth 5)
}

# ------------------------------------------------------------------------------
# QUẢN LÝ TIẾN TRÌNH (PID + thời điểm khởi chạy để không dừng nhầm PID bị Windows tái sử dụng)
# ------------------------------------------------------------------------------
function Get-ProcessIdentity([System.Diagnostics.Process]$Process) {
    if (-not $Process) { return $null }
    try { $startTime = $Process.StartTime.ToString("o") } catch { $startTime = "" }
    return @{ pid = $Process.Id; startTime = $startTime }
}

function Resolve-TrackedProcess($Identity) {
    if (-not $Identity -or -not $Identity.pid) { return $null }
    $proc = Get-Process -Id ([int]$Identity.pid) -ErrorAction SilentlyContinue
    if (-not $proc) { return $null }
    try { $actualStart = $proc.StartTime.ToString("o") } catch { return $null }
    if ($actualStart -ne $Identity.startTime) { return $null }
    return $proc
}

# Dừng cả cây tiến trình (cmd.exe bọc ngoài + máy chủ thật bên trong)
function Stop-ProcessTree([int]$ProcessId) {
    if ($ProcessId -le 4 -or $ProcessId -eq $PID) { return }
    if (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) {
        & taskkill.exe /PID $ProcessId /T /F *> $null
    }
}

function Test-PortListening([int]$Port) {
    return [bool](Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue)
}

function Stop-PortOwners([int[]]$Ports, [scriptblock]$Logger = $null) {
    foreach ($port in $Ports) {
        $owners = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty OwningProcess -Unique
        foreach ($ownerPid in $owners) {
            if ($ownerPid -le 4 -or $ownerPid -eq $PID) { continue }
            $proc = Get-Process -Id $ownerPid -ErrorAction SilentlyContinue
            $name = if ($proc) { $proc.ProcessName } else { "?" }
            $message = "Giai phong cong {0}: dung tien trinh {1} (PID {2})" -f $port, $name, $ownerPid
            if ($Logger) { & $Logger $message } else { Write-Host "[STOP] $message" -ForegroundColor DarkYellow }
            Stop-ProcessTree $ownerPid
        }
    }
}

# Chỉ dừng cloudflared.exe của dự án (không đụng tới cloudflared khác người dùng đang chạy)
function Stop-BundledCloudflared {
    Get-Process -Name "cloudflared" -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -eq $Cloudflared } |
        ForEach-Object { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }
}

# Đóng lớp đường hầm Internet (tiến trình giám sát tunnel + cloudflared), không ảnh hưởng daemon local
function Stop-TunnelLayer {
    $state = Read-JsonState $TunnelStateFile
    if ($state) {
        $supervisor = Resolve-TrackedProcess $state.supervisor
        if ($supervisor -and $supervisor.Id -ne $PID) {
            Stop-Process -Id $supervisor.Id -Force -ErrorAction SilentlyContinue
        }
        $tunnel = Resolve-TrackedProcess $state.tunnel
        if ($tunnel) { Stop-ProcessTree $tunnel.Id }
    }
    Stop-BundledCloudflared
    Remove-Item -Path $TunnelStateFile -Force -ErrorAction SilentlyContinue
}

# ------------------------------------------------------------------------------
# KIỂM TRA SỨC KHỎE & MÔI TRƯỜNG
# ------------------------------------------------------------------------------
function Test-HttpOk([string]$Url, [int]$TimeoutSec = 5) {
    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec $TimeoutSec -ErrorAction Stop
        return ($response.StatusCode -ge 200 -and $response.StatusCode -lt 300)
    } catch {
        return $false
    }
}

function Resolve-PythonCommand {
    foreach ($candidate in @((Join-Path $AiDir ".venv\Scripts\python.exe"), (Join-Path $AiDir "venv\Scripts\python.exe"))) {
        if (Test-Path $candidate) { return $candidate }
    }
    # Bỏ qua "python.exe" giả của Microsoft Store (WindowsApps) vốn chỉ mở cửa hàng ứng dụng
    foreach ($name in @("python", "py")) {
        $cmd = Get-Command $name -CommandType Application -ErrorAction SilentlyContinue |
            Where-Object { $_.Source -notmatch "WindowsApps" } |
            Select-Object -First 1
        if ($cmd) { return $cmd.Source }
    }
    return $null
}

function Get-LanIpAddress {
    # Ưu tiên card mạng đang giữ tuyến mặc định (default route) để tránh chọn nhầm card ảo / VPN
    try {
        $route = Get-NetRoute -DestinationPrefix "0.0.0.0/0" -ErrorAction Stop | Sort-Object RouteMetric | Select-Object -First 1
        if ($route) {
            $ip = Get-NetIPAddress -InterfaceIndex $route.InterfaceIndex -AddressFamily IPv4 -ErrorAction Stop |
                Where-Object { $_.IPAddress -notmatch "^169\.254" } |
                Select-Object -First 1
            if ($ip) { return $ip.IPAddress }
        }
    } catch {
        # Chuyển sang cách dò dự phòng bên dưới
    }
    $fallback = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object { $_.InterfaceAlias -notmatch "Loopback|vEthernet|WSL" -and $_.IPAddress -notmatch "^169\.254" } |
        Select-Object -First 1
    if ($fallback) { return $fallback.IPAddress }
    return "localhost"
}

# ------------------------------------------------------------------------------
# CLOUDFLARE TUNNEL
# ------------------------------------------------------------------------------
# Tải cloudflared.exe chính thức từ GitHub (chỉ lần đầu) vào scripts/local-vps/bin
function Install-Cloudflared([scriptblock]$Logger = $null) {
    if (Test-Path $Cloudflared) { return $true }
    New-Item -ItemType Directory -Force -Path $BinDir | Out-Null

    $is32BitOs = ($env:PROCESSOR_ARCHITECTURE -eq "x86") -and (-not $env:PROCESSOR_ARCHITEW6432)
    $asset = if ($is32BitOs) { "cloudflared-windows-386.exe" } else { "cloudflared-windows-amd64.exe" }
    $downloadUrl = "https://github.com/cloudflare/cloudflared/releases/latest/download/$asset"
    $tempFile = "$Cloudflared.download"
    $log = { param($message) if ($Logger) { & $Logger $message } else { Write-Host $message } }

    & $log "Dang tai Cloudflare Tunnel ($asset) tu GitHub chinh thuc..."
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $downloadUrl -OutFile $tempFile -UseBasicParsing -ErrorAction Stop
        if ((Get-Item $tempFile).Length -lt 1MB) { throw "Tep tai ve khong hop le (dung luong qua nho)." }
        Move-Item -Path $tempFile -Destination $Cloudflared -Force
        & $log "Da tai xong cloudflared.exe"
        return $true
    } catch {
        Remove-Item -Path $tempFile -Force -ErrorAction SilentlyContinue
        & $log ("Khong tai duoc cloudflared: {0}" -f $_.Exception.Message)
        return $false
    }
}

# Link Quick Tunnel cuối cùng xuất hiện trong log của lần chạy cloudflared hiện tại
function Find-QuickTunnelUrl([string]$LogPath) {
    return [regex]::Matches((Read-SharedFile $LogPath), "https://[a-z0-9-]+\.trycloudflare\.com") |
        ForEach-Object { $_.Value } |
        Where-Object { $_ -ne "https://api.trycloudflare.com" } |
        Select-Object -Last 1
}

# Kiểm chứng link công khai thật sự trả 200. Hỏi DNS trực tiếp 1.1.1.1 rồi gắn IP cho curl để không dính
# bộ đệm "không tồn tại" (negative cache) của Windows khi tên miền trycloudflare vừa được cấp phát.
function Test-PublicTunnelUrl([string]$Url, [int]$TimeoutSec = 12) {
    try { $uri = [Uri]$Url } catch { return $false }
    $curlArgs = @("-s", "-o", "NUL", "-w", "%{http_code}", "--max-time", "$TimeoutSec")
    try {
        $record = Resolve-DnsName -Name $uri.Host -Server "1.1.1.1" -Type A -DnsOnly -QuickTimeout -ErrorAction Stop |
            Where-Object { $_.IPAddress } | Select-Object -First 1
        if ($record) { $curlArgs += @("--resolve", ("{0}:443:{1}" -f $uri.Host, $record.IPAddress)) }
    } catch {
        # Mạng chặn DNS 1.1.1.1 -> dùng DNS hệ thống
    }
    $statusCode = & curl.exe @curlArgs ($Url.TrimEnd("/") + "/api/health") 2>$null
    return ($statusCode -eq "200")
}

# Link HTTPS hiện hành nếu daemon vừa kiểm chứng gần đây (file được ghi lại mỗi lần kiểm tra thành công)
function Get-ActiveTunnelUrl([int]$FreshSeconds = 150) {
    if (-not (Test-Path $TunnelUrlFile)) { return $null }
    $item = Get-Item $TunnelUrlFile
    if (((Get-Date) - $item.LastWriteTime).TotalSeconds -gt $FreshSeconds) { return $null }
    $url = (Read-SharedFile $TunnelUrlFile).Trim()
    if ($url -match "^https://") { return $url }
    return $null
}

# Khởi chạy daemon-watchdog.ps1 ẩn hoàn toàn qua start-silent-background.vbs
function Start-SilentDaemon {
    Start-Process -FilePath "wscript.exe" -ArgumentList "`"$SilentLauncher`"" -WindowStyle Hidden
}

function Wait-BackendHealthy([int]$TimeoutSeconds = 240, [switch]$ShowProgress) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-HttpOk $BackendHealthUrl 4) { return $true }
        if ($ShowProgress) { Write-Host "." -NoNewline -ForegroundColor DarkGray }
        Start-Sleep -Seconds 2
    }
    return $false
}
