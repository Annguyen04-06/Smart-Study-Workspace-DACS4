<#
==============================================================================
 SMART STUDY WORKSPACE - DAEMON WATCHDOG 24/7 (CHẠY NGẦM, KHÔNG CẦN QUYỀN ADMIN)
==============================================================================
 Giám sát và tự hồi phục 3 dịch vụ, mỗi dịch vụ chạy trong console ẩn riêng nên đóng bất kỳ
 cửa sổ nào cũng KHÔNG làm hệ thống ngừng:
   - Python AI Microservice : 127.0.0.1:8000 (chỉ Backend gọi nội bộ)        -> logs/ai.log
   - C# Backend All-in-One  : 0.0.0.0:5000 và 0.0.0.0:5500 (API + giao diện) -> logs/backend.log
   - Cloudflare HTTPS Tunnel: https://xxxx.trycloudflare.com -> 127.0.0.1:5500 -> logs/tunnel.log
       + Có biến môi trường CLOUDFLARE_TUNNEL_TOKEN: chạy tên miền cố định (cloudflared tunnel run),
         khai báo kèm CLOUDFLARE_TUNNEL_PUBLIC_URL (ví dụ https://study.ten-mien.com).
       + Không có token: Quick Tunnel, đọc link trong log, kiểm chứng trả 200 rồi ghi current-tunnel-url.txt
         (ghi lại mỗi 60 giây làm "nhịp tim"; Cloudflare thu hồi link => tự mở đường hầm mới).
       + Tạo file tunnel.disabled (run-local-vps.bat -Stop) để tắt đường hầm Internet.
 Backend chạy bản Release đã publish (scripts/local-vps/publish-backend) nên không khóa file khi code,
 với --contentRoot trỏ về thư mục dự án để dùng chung appsettings và wwwroot/uploads (PDF, Avatar).

 Cách dùng:
   (không tham số)       : vòng giám sát — được start-silent-background.vbs gọi ẩn hoàn toàn
   -Start                : bật daemon ẩn (nếu chưa chạy), chờ sẵn sàng và in link truy cập
   -Stop                 : tắt sạch Watchdog, Backend, AI Service và Cloudflare Tunnel
   -Status               : xem trạng thái các cổng, Watchdog và tự khởi động
   -Redeploy             : tắt -> dotnet publish bản mới -> bật lại ngầm (sau khi sửa code Backend)
   -InstallAutostart     : tự chạy ngầm cùng Windows (Startup\SmartStudyAutoStart.vbs)
   -UninstallAutostart   : gỡ tự chạy cùng Windows
==============================================================================
#>
[CmdletBinding()]
param(
    [switch]$Start,
    [switch]$Stop,
    [switch]$Status,
    [switch]$Redeploy,
    [switch]$InstallAutostart,
    [switch]$UninstallAutostart
)

$ErrorActionPreference = "Continue"
$ProgressPreference    = "SilentlyContinue"
. (Join-Path $PSScriptRoot "lib\LocalVps.Common.ps1")

$CheckIntervalSeconds   = 15
$UnhealthyRestartLimit  = 3
$RestartBudgetPerWindow = 5
$RestartWindowMinutes   = 10
$MutexName              = "Local\SmartStudyDaemonWatchdog"
$DaemonLog              = Join-Path $LogDir "daemon.log"
$AiLog                  = Join-Path $LogDir "ai.log"
$BackendLog             = Join-Path $LogDir "backend.log"
$PublishLog             = Join-Path $LogDir "publish.log"
$TunnelLog              = Join-Path $LogDir "tunnel.log"
$TunnelVerifyIntervalSeconds = 60
$TunnelFailureLimit     = 3
$NewTunnelFailureLimit  = 8      # Link mới cấp cần thêm thời gian lan truyền DNS (8 x 15 giây)
$TunnelUrlTimeoutSeconds = 90
$ManagementSwitches     = "-(Start|Stop|Status|Redeploy|InstallAutostart|UninstallAutostart)\b"

function Write-DaemonLog([string]$Message) {
    $line = "{0} | {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $Message
    try { Add-Content -Path $DaemonLog -Value $line -Encoding UTF8 } catch { }
    Write-Host $line
}

# Tìm các tiến trình Watchdog đang chạy vòng giám sát (bỏ qua các lệnh quản lý -Start/-Stop...)
function Get-WatchdogProcesses {
    Get-CimInstance Win32_Process -Filter "Name = 'powershell.exe' OR Name = 'pwsh.exe'" -ErrorAction SilentlyContinue |
        Where-Object {
            $_.ProcessId -ne $PID -and
            $_.CommandLine -match "daemon-watchdog\.ps1" -and
            $_.CommandLine -notmatch $ManagementSwitches
        }
}

# ------------------------------------------------------------------------------
# BIÊN DỊCH & KHỞI CHẠY DỊCH VỤ
# ------------------------------------------------------------------------------
function Invoke-BackendPublish {
    Write-DaemonLog "Dang bien dich ban Release: dotnet publish -> $PublishDir"
    Invoke-LogRotation $PublishLog 2MB
    $output = & dotnet publish "$BackendCsproj" -c Release -o "$PublishDir" --nologo 2>&1
    $exitCode = $LASTEXITCODE
    Add-Content -Path $PublishLog -Value ("===== {0}`r`n{1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), ($output | Out-String)) -Encoding UTF8
    if ($exitCode -ne 0) {
        Write-DaemonLog "LOI: dotnet publish that bai (exit $exitCode). Chi tiet: $PublishLog"
        return $false
    }
    Write-DaemonLog "Publish thanh cong."
    return $true
}

# Bọc tiến trình bằng cmd.exe ẩn để gộp stdout + stderr vào 1 file log (UTF-8)
function Start-HiddenLoggedProcess([string]$Executable, [string]$Arguments, [string]$WorkingDirectory, [string]$LogPath) {
    Invoke-LogRotation $LogPath
    Add-Content -Path $LogPath -Value ("===== {0} | Khoi dong: {1} {2}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $Executable, $Arguments) -Encoding UTF8
    $commandLine = "/d /s /c `"chcp 65001>nul & `"$Executable`" $Arguments >> `"$LogPath`" 2>&1`""
    return Start-Process -FilePath "cmd.exe" -ArgumentList $commandLine -WorkingDirectory $WorkingDirectory -WindowStyle Hidden -PassThru
}

function Start-AiService {
    $python = Resolve-PythonCommand
    if (-not $python) {
        Write-DaemonLog "LOI: Khong tim thay Python (ai-service\.venv, ai-service\venv hoac PATH)."
        return $null
    }
    return Start-HiddenLoggedProcess $python "-m uvicorn app.main:app --host 127.0.0.1 --port $AiPort" $AiDir $AiLog
}

function Start-BackendService {
    if (-not (Test-Path $BackendExe) -and -not (Test-Path $BackendDll)) {
        if (-not (Invoke-BackendPublish)) { return $null }
    }
    $appArguments = "--contentRoot `"$BackendProjectDir`" --urls `"$BackendUrls`""
    # Ưu tiên SmartStudy.API.exe để luật Windows Firewall chỉ áp cho đúng ứng dụng này (không mở cho mọi app .NET)
    if (Test-Path $BackendExe) {
        return Start-HiddenLoggedProcess $BackendExe $appArguments $BackendProjectDir $BackendLog
    }
    $dotnet = (Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1).Source
    return Start-HiddenLoggedProcess $dotnet "`"$BackendDll`" $appArguments" $BackendProjectDir $BackendLog
}

$Services = [ordered]@{
    ai = @{
        Name = "Python AI Microservice (port $AiPort)"; Ports = @($AiPort); HealthUrl = $AiHealthUrl; LogPath = $AiLog
        GraceSeconds = 90; Starter = { Start-AiService }
        Process = $null; StartedAt = $null; Failures = 0; IsHealthy = $false; Restarts = @(); Throttled = $false
    }
    backend = @{
        Name = "C# Backend All-in-One (port 5000 + 5500)"; Ports = $BackendPorts; HealthUrl = $BackendHealthUrl; LogPath = $BackendLog
        GraceSeconds = 240; Starter = { Start-BackendService }
        Process = $null; StartedAt = $null; Failures = 0; IsHealthy = $false; Restarts = @(); Throttled = $false
    }
}

# ------------------------------------------------------------------------------
# CLOUDFLARE HTTPS TUNNEL (link công khai để điện thoại cài App 1 chạm & dùng Micro)
# ------------------------------------------------------------------------------
$Tunnel = @{
    Name = "Cloudflare HTTPS Tunnel"; LogPath = $TunnelLog
    Process = $null; StartedAt = $null; Url = $null; LastVerifiedAt = $null; Failures = 0
    Restarts = @(); Throttled = $false; IsNamedTunnel = $false; NextInstallAttempt = (Get-Date)
}

function Get-SafeExitCode($Process) {
    try { return $Process.ExitCode } catch { return "?" }
}

# Dừng các cloudflared.exe của dự án không thuộc đường hầm daemon đang giữ (ví dụ cửa sổ run-local-vps bản cũ)
function Stop-OrphanCloudflared([int]$KeepParentPid = 0) {
    Get-CimInstance Win32_Process -Filter "Name = 'cloudflared.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.ExecutablePath -eq $Cloudflared -and $_.ParentProcessId -ne $KeepParentPid } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
}

function Stop-LegacyTunnelSupervisor {
    $legacy = Read-JsonState $TunnelStateFile
    if ($legacy) {
        $supervisor = Resolve-TrackedProcess $legacy.supervisor
        if ($supervisor -and $supervisor.Id -ne $PID) { Stop-Process -Id $supervisor.Id -Force -ErrorAction SilentlyContinue }
    }
    Remove-Item -Path $TunnelStateFile -Force -ErrorAction SilentlyContinue
}

function Clear-TunnelUrlFile {
    Remove-Item -Path $TunnelUrlFile -Force -ErrorAction SilentlyContinue
}

function Stop-DaemonTunnel([string]$Reason) {
    if ($Tunnel.Process -and -not $Tunnel.Process.HasExited) {
        Write-DaemonLog "Dung Cloudflare Tunnel: $Reason"
        Stop-ProcessTree $Tunnel.Process.Id
    }
    $Tunnel.Process = $null
    $Tunnel.Url = $null
    $Tunnel.LastVerifiedAt = $null
    $Tunnel.Failures = 0
    Clear-TunnelUrlFile
    Save-DaemonState
}

function Start-DaemonTunnel {
    if (Test-Path $TunnelLog) {
        Move-Item -Path $TunnelLog -Destination "$TunnelLog.old" -Force -ErrorAction SilentlyContinue
    }
    if ($env:CLOUDFLARE_TUNNEL_TOKEN) {
        # Token chỉ truyền qua biến môi trường TUNNEL_TOKEN cho riêng cloudflared (không lộ trên dòng lệnh / file log)
        $Tunnel.IsNamedTunnel = $true
        $env:TUNNEL_TOKEN = $env:CLOUDFLARE_TUNNEL_TOKEN
        try {
            return Start-HiddenLoggedProcess $Cloudflared "tunnel --no-autoupdate run" $VpsRoot $TunnelLog
        } finally {
            Remove-Item Env:\TUNNEL_TOKEN -ErrorAction SilentlyContinue
        }
    }
    $Tunnel.IsNamedTunnel = $false
    return Start-HiddenLoggedProcess $Cloudflared "tunnel --url $TunnelTargetUrl --no-autoupdate" $VpsRoot $TunnelLog
}

function Resolve-TunnelPublicUrl {
    if ($Tunnel.IsNamedTunnel) {
        $publicUrl = "$env:CLOUDFLARE_TUNNEL_PUBLIC_URL".Trim().TrimEnd("/")
        if ($publicUrl -match "^https://") { return $publicUrl }
        return $null
    }
    return Find-QuickTunnelUrl $TunnelLog
}

function Invoke-TunnelCheck {
    if (Test-Path $TunnelDisabledFlag) {
        if ($Tunnel.Process) { Stop-DaemonTunnel "nguoi dung tat duong ham Internet (tunnel.disabled)" }
        return
    }
    if (-not (Test-Path $Cloudflared)) {
        if ((Get-Date) -lt $Tunnel.NextInstallAttempt) { return }
        $Tunnel.NextInstallAttempt = (Get-Date).AddMinutes(10)
        if (-not (Install-Cloudflared { param($message) Write-DaemonLog $message })) { return }
    }
    # Chỉ cấp link công khai khi Backend cổng 5500 đã sẵn sàng phục vụ
    if (-not $Services.backend.IsHealthy) { return }

    $proc = $Tunnel.Process
    if (-not $proc -or $proc.HasExited) {
        if ($proc) {
            Write-DaemonLog ("Cloudflare Tunnel da dung (exit {0}). Chi tiet: {1}" -f (Get-SafeExitCode $proc), $TunnelLog)
        }
        Clear-TunnelUrlFile
        if (-not (Test-RestartBudget $Tunnel)) { return }
        Write-DaemonLog "Khoi dong Cloudflare HTTPS Tunnel -> $TunnelTargetUrl ..."
        $Tunnel.Process        = Start-DaemonTunnel
        $Tunnel.StartedAt      = Get-Date
        $Tunnel.Url            = $null
        $Tunnel.LastVerifiedAt = $null
        $Tunnel.Failures       = 0
        Save-DaemonState
        return
    }

    if (-not $Tunnel.Url) {
        $Tunnel.Url = Resolve-TunnelPublicUrl
        if (-not $Tunnel.Url) {
            if ($Tunnel.IsNamedTunnel) {
                Write-DaemonLog "Thieu bien CLOUDFLARE_TUNNEL_PUBLIC_URL (https://...) cho ten mien co dinh -> chua ghi duoc link."
            } elseif (((Get-Date) - $Tunnel.StartedAt).TotalSeconds -gt $TunnelUrlTimeoutSeconds) {
                Stop-DaemonTunnel "Cloudflare khong cap link sau $TunnelUrlTimeoutSeconds giay (xem $TunnelLog)"
            }
            return
        }
        Write-DaemonLog "Cloudflare da cap phat link: $($Tunnel.Url) (dang kiem chung...)"
    }

    # Link chưa xác minh: thử mỗi chu kỳ 15 giây; đã xác minh: kiểm tra lại mỗi 60 giây làm "nhịp tim"
    $isVerified = [bool]$Tunnel.LastVerifiedAt
    if ($isVerified -and ((Get-Date) - $Tunnel.LastVerifiedAt).TotalSeconds -lt $TunnelVerifyIntervalSeconds) { return }

    if (Test-PublicTunnelUrl $Tunnel.Url) {
        if (-not $isVerified) { Write-DaemonLog "Link HTTPS da hoat dong (200 OK): $($Tunnel.Url)" }
        Write-Utf8File $TunnelUrlFile ($Tunnel.Url + [Environment]::NewLine)
        $Tunnel.LastVerifiedAt = Get-Date
        $Tunnel.Failures = 0
        return
    }

    $Tunnel.Failures++
    $limit = if ($isVerified) { $TunnelFailureLimit } else { $NewTunnelFailureLimit }
    if ($isVerified) {
        Write-DaemonLog ("Link {0} khong phan hoi (lan {1}/{2})." -f $Tunnel.Url, $Tunnel.Failures, $limit)
    }
    if ($Tunnel.Failures -ge $limit) {
        Stop-DaemonTunnel "Cloudflare da thu hoi / link khong phan hoi: $($Tunnel.Url)"
    }
}

# ------------------------------------------------------------------------------
# VÒNG GIÁM SÁT TỰ HỒI PHỤC
# ------------------------------------------------------------------------------
function Save-DaemonState {
    if (-not $Script:DaemonState) { return }
    $Script:DaemonState.ai      = Get-ProcessIdentity $Services.ai.Process
    $Script:DaemonState.backend = Get-ProcessIdentity $Services.backend.Process
    $Script:DaemonState.tunnel  = Get-ProcessIdentity $Tunnel.Process
    Write-JsonState $DaemonStateFile $Script:DaemonState
}

# Chống vòng lặp khởi động lại vô hạn (ví dụ lỗi cấu hình): tối đa 5 lần trong 10 phút
function Test-RestartBudget($Service) {
    $windowStart = (Get-Date).AddMinutes(-$RestartWindowMinutes)
    $Service.Restarts = @($Service.Restarts | Where-Object { $_ -ge $windowStart })
    if ($Service.Restarts.Count -ge $RestartBudgetPerWindow) {
        if (-not $Service.Throttled) {
            Write-DaemonLog ("{0} khoi dong lai {1} lan trong {2} phut -> tam dung de tranh vong lap. Xem log: {3}" -f $Service.Name, $RestartBudgetPerWindow, $RestartWindowMinutes, $Service.LogPath)
            $Service.Throttled = $true
        }
        return $false
    }
    $Service.Throttled = $false
    $Service.Restarts += (Get-Date)
    return $true
}

function Invoke-ServiceCheck($Service) {
    $proc = $Service.Process
    if (-not $proc -or $proc.HasExited) {
        if ($proc) {
            Write-DaemonLog ("{0} da dung (exit {1}). Chi tiet: {2}" -f $Service.Name, $proc.ExitCode, $Service.LogPath)
        }
        if (-not (Test-RestartBudget $Service)) { return }
        Stop-PortOwners $Service.Ports { param($message) Write-DaemonLog $message }
        Write-DaemonLog "Khoi dong $($Service.Name)..."
        $Service.Process   = & $Service.Starter
        $Service.StartedAt = Get-Date
        $Service.Failures  = 0
        $Service.IsHealthy = $false
        Save-DaemonState
        return
    }

    $ageSeconds = [int]((Get-Date) - $Service.StartedAt).TotalSeconds
    $portsReady = -not ($Service.Ports | Where-Object { -not (Test-PortListening $_) })
    if ($portsReady -and (Test-HttpOk $Service.HealthUrl 5)) {
        if (-not $Service.IsHealthy) {
            Write-DaemonLog "$($Service.Name) da san sang sau $ageSeconds giay."
            $Service.IsHealthy = $true
        }
        $Service.Failures = 0
        return
    }

    # Đang trong thời gian khởi động (build/DbSeeder/nạp model) thì chưa tính là lỗi
    if ($ageSeconds -lt $Service.GraceSeconds) { return }

    $Service.Failures++
    Write-DaemonLog ("{0} khong phan hoi kiem tra suc khoe (lan {1}/{2})." -f $Service.Name, $Service.Failures, $UnhealthyRestartLimit)
    if ($Service.Failures -ge $UnhealthyRestartLimit) {
        Write-DaemonLog "$($Service.Name) bi treo -> dung de khoi dong lai."
        Stop-ProcessTree $proc.Id
        $Service.IsHealthy = $false
    }
}

function Stop-PreviousDaemonChildren($Previous) {
    if (-not $Previous) { return }
    foreach ($identity in @($Previous.backend, $Previous.ai)) {
        $proc = Resolve-TrackedProcess $identity
        if ($proc) {
            Write-DaemonLog "Dung tien trinh con con sot lai tu phien truoc (PID $($proc.Id))."
            Stop-ProcessTree $proc.Id
        }
    }
}

# Sau Redeploy, đường hầm của phiên trước vẫn chạy => nhận lại để giữ nguyên link HTTPS cho điện thoại
function Initialize-TunnelFromPrevious($Previous) {
    Stop-LegacyTunnelSupervisor
    $previousTunnel = if ($Previous) { Resolve-TrackedProcess $Previous.tunnel } else { $null }
    if ($previousTunnel -and -not (Test-Path $TunnelDisabledFlag)) {
        Stop-OrphanCloudflared -KeepParentPid $previousTunnel.Id
        $Tunnel.Process   = $previousTunnel
        $Tunnel.StartedAt = Get-Date
        Write-DaemonLog "Tiep quan Cloudflare Tunnel dang chay tu phien truoc (PID $($previousTunnel.Id)) -> giu nguyen link."
        return
    }
    if ($previousTunnel) { Stop-ProcessTree $previousTunnel.Id }
    Stop-OrphanCloudflared
    Clear-TunnelUrlFile
}

function Invoke-DaemonLoop {
    $createdNew = $false
    $mutex = New-Object System.Threading.Mutex($true, $MutexName, [ref]$createdNew)
    if (-not $createdNew) {
        Write-DaemonLog "Watchdog da dang chay o tien trinh khac -> bo qua lan khoi dong trung lap."
        $mutex.Dispose()
        return
    }

    try {
        Invoke-LogRotation $DaemonLog 2MB
        Write-DaemonLog "===== Daemon Watchdog khoi dong (PID $PID, kiem tra moi $CheckIntervalSeconds giay) ====="
        $env:PYTHONUTF8 = "1"
        $env:PYTHONIOENCODING = "utf-8"
        # Backend đọc link HTTPS từ đúng file daemon ghi (GET /api/system/mobile-access)
        $env:MobileAccess__TunnelUrlFile = $TunnelUrlFile

        $previous = Read-JsonState $DaemonStateFile
        Stop-PreviousDaemonChildren $previous
        Initialize-TunnelFromPrevious $previous
        Stop-PortOwners (@($AiPort) + $BackendPorts) { param($message) Write-DaemonLog $message }

        $Script:DaemonState = @{
            daemon    = Get-ProcessIdentity (Get-Process -Id $PID)
            ai        = $null
            backend   = $null
            tunnel    = $null
            startedAt = (Get-Date).ToString("o")
            updatedAt = ""
        }
        Save-DaemonState

        while ($true) {
            foreach ($service in $Services.Values) {
                Invoke-ServiceCheck $service
            }
            Invoke-TunnelCheck
            Start-Sleep -Seconds $CheckIntervalSeconds
        }
    } finally {
        $mutex.ReleaseMutex()
        $mutex.Dispose()
    }
}

# ------------------------------------------------------------------------------
# LỆNH QUẢN LÝ
# ------------------------------------------------------------------------------
function Stop-Everything([switch]$KeepTunnel) {
    # KeepTunnel (Redeploy): giữ cloudflared chạy để daemon mới tiếp quản, link HTTPS không đổi
    if (-not $KeepTunnel) {
        Stop-TunnelLayer
        Remove-Item -Path $TunnelUrlFile -Force -ErrorAction SilentlyContinue
    }

    # Dừng Watchdog trước để nó không tự bật lại các dịch vụ
    $state = Read-JsonState $DaemonStateFile
    $daemonFromState = if ($state) { Resolve-TrackedProcess $state.daemon } else { $null }
    $watchdogIds = @(Get-WatchdogProcesses | Select-Object -ExpandProperty ProcessId)
    if ($daemonFromState) { $watchdogIds += $daemonFromState.Id }
    foreach ($watchdogId in ($watchdogIds | Select-Object -Unique)) {
        if ($watchdogId -ne $PID) {
            Write-Host "[STOP] Dung Watchdog (PID $watchdogId)" -ForegroundColor DarkYellow
            Stop-Process -Id $watchdogId -Force -ErrorAction SilentlyContinue
        }
    }

    if ($state) {
        $children = @($state.backend, $state.ai)
        if (-not $KeepTunnel) { $children += $state.tunnel }
        foreach ($identity in $children) {
            $proc = Resolve-TrackedProcess $identity
            if ($proc) { Stop-ProcessTree $proc.Id }
        }
    }
    Stop-PortOwners (@($AiPort) + $BackendPorts)
    # Redeploy cần giữ lại thông tin đường hầm trong file trạng thái để daemon mới tiếp quản
    if (-not $KeepTunnel) {
        Remove-Item -Path $DaemonStateFile -Force -ErrorAction SilentlyContinue
    }
}

function Show-AccessLinks {
    $lanIp = Get-LanIpAddress
    $httpsUrl = Get-ActiveTunnelUrl
    Write-Host ""
    Write-Host "  Laptop      : http://localhost:5500   (hoac http://localhost:5000)" -ForegroundColor Cyan
    Write-Host "  Dien thoai  : http://${lanIp}:5500   (cung mang Wi-Fi)" -ForegroundColor Cyan
    if ($httpsUrl) {
        Write-Host "  HTTPS 4G/Wi-Fi (cai App 1 cham): $httpsUrl" -ForegroundColor Green
    }
    Write-Host "  Swagger API : http://localhost:5000/swagger" -ForegroundColor DarkGray
    Write-Host "  Nhat ky     : $LogDir" -ForegroundColor DarkGray
    Write-Host ""
}

function Start-Background {
    if (Get-WatchdogProcesses) {
        Write-Host "[OK] Watchdog dang chay ngam." -ForegroundColor Green
    } else {
        Write-Host "[*] Dang bat he thong chay ngam (an hoan toan)..." -ForegroundColor Yellow
        Start-SilentDaemon
    }

    Write-Host "[*] Dang cho Backend + AI san sang" -NoNewline -ForegroundColor Gray
    $deadline = (Get-Date).AddSeconds(240)
    $ready = $false
    while ((Get-Date) -lt $deadline) {
        if ((Test-HttpOk $BackendHealthUrl 4) -and (Test-PortListening 5500) -and (Test-HttpOk $AiHealthUrl 4)) { $ready = $true; break }
        Write-Host "." -NoNewline -ForegroundColor DarkGray
        Start-Sleep -Seconds 2
    }
    Write-Host ""
    if ($ready) {
        Write-Host "[OK] He thong da san sang va dang chay ngam 24/7." -ForegroundColor Green
        Show-AccessLinks
        return $true
    }
    Write-Host "[LOI] He thong chua san sang sau 240 giay. Nhat ky gan nhat:" -ForegroundColor Red
    Write-Host (Get-LogTail $DaemonLog 10) -ForegroundColor DarkGray
    Write-Host (Get-LogTail $BackendLog 15) -ForegroundColor DarkGray
    return $false
}

function Show-Status {
    Write-Host "=== SMART STUDY WORKSPACE - TRANG THAI HE THONG CHAY NGAM ===" -ForegroundColor Cyan
    $watchdogs = @(Get-WatchdogProcesses)
    Write-Host ("Watchdog        : {0}" -f $(if ($watchdogs.Count) { "DANG CHAY (PID $($watchdogs[0].ProcessId))" } else { "KHONG CHAY" }))
    foreach ($check in @(
        @{ Label = "Backend :5000  "; Port = 5000; Url = $BackendHealthUrl },
        @{ Label = "Backend :5500  "; Port = 5500; Url = "http://127.0.0.1:5500/api/health" },
        @{ Label = "AI Service :8000"; Port = $AiPort; Url = $AiHealthUrl }
    )) {
        $listening = Test-PortListening $check.Port
        $healthy = $listening -and (Test-HttpOk $check.Url 4)
        Write-Host ("{0}: {1}" -f $check.Label, $(if ($healthy) { "OK (HTTP 200)" } elseif ($listening) { "DANG KHOI DONG / LOI" } else { "TAT" }))
    }
    $httpsUrl = Get-ActiveTunnelUrl
    $tunnelStatus = if (Test-Path $TunnelDisabledFlag) {
        "TAT (tunnel.disabled - bat lai bang run-local-vps.bat)"
    } elseif ($httpsUrl) {
        "OK $httpsUrl (kiem chung luc $((Get-Item $TunnelUrlFile).LastWriteTime.ToString('HH:mm:ss')))"
    } else {
        "DANG MO / CHUA KIEM CHUNG (xem logs\tunnel.log)"
    }
    Write-Host ("HTTPS Tunnel    : {0}" -f $tunnelStatus)
    Write-Host ("Tu chay cung Windows: {0}" -f $(if (Test-Path $AutostartFile) { "BAT ($AutostartFile)" } else { "TAT" }))
    Show-AccessLinks
}

function Install-Autostart {
    # File .vbs ghi dạng UTF-16 LE để Windows Script Host đọc đúng cả đường dẫn có dấu tiếng Việt
    $vbsContent = @(
        "' Smart Study Workspace - Tu khoi dong he thong chay ngam cung Windows (khong can quyen Admin)",
        "' Tao boi scripts\local-vps\enable-autostart-vps.bat - Go bo bang disable-autostart-vps.bat",
        ('CreateObject("WScript.Shell").Run "wscript.exe ""' + $SilentLauncher + '""", 0, False')
    ) -join "`r`n"
    [System.IO.File]::WriteAllText($AutostartFile, $vbsContent + "`r`n", [System.Text.Encoding]::Unicode)
    Remove-Item -Path $LegacyAutostart -Force -ErrorAction SilentlyContinue
    Write-Host "[OK] Da bat tu chay ngam cung Windows: $AutostartFile" -ForegroundColor Green
}

function Uninstall-Autostart {
    $removed = $false
    foreach ($path in @($AutostartFile, $LegacyAutostart)) {
        if (Test-Path $path) {
            Remove-Item -Path $path -Force
            $removed = $true
        }
    }
    if ($removed) {
        Write-Host "[OK] Da tat tu chay cung Windows. He thong dang chay (neu co) van hoat dong; dung stop-background.bat de tat." -ForegroundColor Green
    } else {
        Write-Host "[INFO] Tu chay cung Windows chua duoc bat." -ForegroundColor Yellow
    }
}

# ==============================================================================
# CHƯƠNG TRÌNH CHÍNH
# ==============================================================================
if ($Stop) {
    Write-Host "[STOP] Dang tat Watchdog, Backend, AI Service va Cloudflare Tunnel..." -ForegroundColor Yellow
    Stop-Everything
    Write-Host "[OK] Da tat sach. Cong 5000, 5500, 8000 da duoc giai phong." -ForegroundColor Green
    exit 0
}
if ($Status)             { Show-Status; exit 0 }
if ($InstallAutostart)   { Install-Autostart; exit 0 }
if ($UninstallAutostart) { Uninstall-Autostart; exit 0 }
if ($Start)              { if (Start-Background) { exit 0 } else { exit 1 } }

if ($Redeploy) {
    Write-Host "[1/3] Tat Backend + AI dang chay ngam (giu nguyen Cloudflare Tunnel de link HTTPS khong doi)..." -ForegroundColor Yellow
    Stop-Everything -KeepTunnel
    Write-Host "[2/3] Bien dich ban Release moi (dotnet publish)..." -ForegroundColor Yellow
    if (-not (Invoke-BackendPublish)) {
        Write-Host (Get-LogTail $PublishLog 25) -ForegroundColor DarkRed
        exit 1
    }
    Write-Host "[3/3] Bat lai he thong chay ngam..." -ForegroundColor Yellow
    if (Start-Background) { exit 0 } else { exit 1 }
}

Invoke-DaemonLoop
