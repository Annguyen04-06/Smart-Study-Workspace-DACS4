<#
===============================================================================
 Smart Study Workspace — Cài đặt Windows Background Service (Chạy ngầm 24/7)
 Đăng ký các Windows Scheduled Tasks chạy ẩn hoàn toàn (Hidden), tự khởi động
 khi bật máy (AtStartup / AtLogOn) và tự khởi động lại nếu gặp sự cố:
   1. SmartStudy-AiService       (Python FastAPI trên 127.0.0.1:8000)
   2. SmartStudy-BackendCore     (C# .NET 8 All-in-One trên 0.0.0.0:5000)
   3. SmartStudy-CloudflareTunnel (Tùy chọn: gắn tên miền HTTPS cố định vĩnh viễn)

 Cách dùng:
   powershell -ExecutionPolicy Bypass -File scripts\background-service\install-windows-service.ps1
   Hoặc có gắn Cloudflare Named Tunnel Token:
   powershell -ExecutionPolicy Bypass -File scripts\background-service\install-windows-service.ps1 -CloudflareTunnelToken "eyJhIjoi..."
===============================================================================
#>
[CmdletBinding()]
param(
    [string]$CloudflareTunnelToken = $env:CLOUDFLARE_TUNNEL_TOKEN,
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

$BgDir       = $PSScriptRoot
$ProjectRoot = (Resolve-Path (Join-Path $BgDir "..\..")).Path
$WorkerPath  = Join-Path $BgDir "run-hidden-worker.ps1"
$LogDir      = Join-Path $BgDir "logs"
$PublishDir  = Join-Path $BgDir "published-backend"

New-Item -ItemType Directory -Path $LogDir -Force | Out-Null

Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host "  SMART STUDY WORKSPACE — CÀI ĐẶT DỊCH VỤ CHẠY NGẦM 24/7 TRÊN WINDOWS          " -ForegroundColor Cyan
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host "[*] Thư mục dự án: $ProjectRoot"

# Kiểm tra quyền Administrator để chọn Trigger AtStartup (chạy cả trước khi đăng nhập Windows) hay AtLogOn
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
$isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if ($isAdmin) {
    Write-Host "[*] Đang chạy với quyền Administrator -> Đăng ký tự khởi động ngay khi bật máy (AtStartup)." -ForegroundColor Green
} else {
    Write-Host "[!] Không chạy với quyền Administrator -> Đăng ký tự khởi động khi đăng nhập Windows (AtLogOn)." -ForegroundColor Yellow
    Write-Host "    (Mẹo: Chạy PowerShell 'Run as Administrator' nếu muốn dịch vụ chạy ngay cả trước khi mở khóa màn hình)." -ForegroundColor DarkGray
}

# 1. Dọn dẹp các tác vụ cũ và cổng đang mở trước khi biên dịch
Write-Host "[1/4] Dừng các dịch vụ cũ nếu đang chạy..." -ForegroundColor Yellow
foreach ($oldTask in @("SmartStudy-AiService", "SmartStudy-BackendCore", "SmartStudy-CloudflareTunnel")) {
    Stop-ScheduledTask -TaskName $oldTask -ErrorAction SilentlyContinue
}
$ports = @(5000, 8000)
$pids = Get-NetTCPConnection -LocalPort $ports -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique
foreach ($p in $pids) {
    if ($p -gt 4) { Stop-Process -Id $p -Force -ErrorAction SilentlyContinue }
}
Start-Sleep -Seconds 1

# 2. Xuất bản Backend Core Release để khởi chạy cực nhanh và nhẹ RAM
if (-not $SkipPublish) {
    Write-Host "[2/4] Biên dịch & xuất bản Backend Core (.NET 8 Release) cho dịch vụ nền..." -ForegroundColor Yellow
    $apiProj = Join-Path $ProjectRoot "backend-core\src\SmartStudy.API\SmartStudy.API.csproj"
    dotnet publish $apiProj -c Release -o $PublishDir /p:UseAppHost=false
    if ($LASTEXITCODE -ne 0) {
        throw "Biên dịch Backend Core thất bại!"
    }

    # Giữ nguyên cấu hình SMTP cục bộ nếu có
    $localSecret = Join-Path $ProjectRoot "backend-core\src\SmartStudy.API\appsettings.Local.json"
    if (Test-Path $localSecret) {
        Copy-Item -Path $localSecret -Destination (Join-Path $PublishDir "appsettings.Local.json") -Force
    }
    $srcMaterials = Join-Path $ProjectRoot "backend-core\src\SmartStudy.API\wwwroot\uploads\materials"
    if (Test-Path $srcMaterials) {
        $dstMaterials = Join-Path $PublishDir "wwwroot\uploads\materials"
        New-Item -ItemType Directory -Path $dstMaterials -Force | Out-Null
        Copy-Item -Path "$srcMaterials\*" -Destination $dstMaterials -Recurse -Force -ErrorAction SilentlyContinue
    }
} else {
    Write-Host "[2/4] Bỏ qua bước publish theo cờ -SkipPublish." -ForegroundColor DarkGray
}

function Register-SmartStudyTask {
    param(
        [string]$TaskName,
        [string]$Role,
        [string]$Description
    )

    $argString = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$WorkerPath`" -Role $Role -ProjectRoot `"$ProjectRoot`""
    $action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument $argString -WorkingDirectory $ProjectRoot

    if ($isAdmin) {
        $trigger = New-ScheduledTaskTrigger -AtStartup
        $taskPrincipal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
    } else {
        $trigger = New-ScheduledTaskTrigger -AtLogOn
        $taskPrincipal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Limited
    }

    $settings = New-ScheduledTaskSettingsSet `
        -Hidden `
        -AllowStartIfOnBatteries `
        -DontStopIfGoingOnBatteries `
        -StartWhenAvailable `
        -MultipleInstances IgnoreNew `
        -ExecutionTimeLimit ([TimeSpan]::Zero) `
        -RestartCount 999 `
        -RestartInterval (New-TimeSpan -Minutes 1)

    Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Principal $taskPrincipal -Settings $settings -Description $Description -Force | Out-Null
    Write-Host "    [OK] Đã đăng ký tác vụ nền: $TaskName" -ForegroundColor Green
}

# 3. Đăng ký Scheduled Tasks
Write-Host "[3/4] Đang đăng ký Windows Scheduled Tasks chạy ẩn..." -ForegroundColor Yellow
Register-SmartStudyTask -TaskName "SmartStudy-AiService" -Role "AiService" -Description "Smart Study Workspace - Python FastAPI AI Microservice (Port 8000)"
Register-SmartStudyTask -TaskName "SmartStudy-BackendCore" -Role "BackendCore" -Description "Smart Study Workspace - ASP.NET Core 8 All-in-One Host (Port 5000)"

# Tùy chọn đăng ký Cloudflare Named Tunnel cố định tên miền HTTPS vĩnh viễn
if (-not [string]::IsNullOrWhiteSpace($CloudflareTunnelToken)) {
    Write-Host "    [*] Phát hiện CLOUDFLARE_TUNNEL_TOKEN -> Cấu hình đường hầm HTTPS tên miền cố định..." -ForegroundColor Cyan
    $tokenFile = Join-Path $BgDir ".cloudflare-token"
    [System.IO.File]::WriteAllText($tokenFile, $CloudflareTunnelToken.Trim(), (New-Object System.Text.UTF8Encoding($false)))

    # Tự tải cloudflared.exe nếu máy chưa có
    $cfBinDir = Join-Path $ProjectRoot "scripts\local-vps\bin"
    $cfExe = Join-Path $cfBinDir "cloudflared.exe"
    if (-not (Test-Path $cfExe) -and -not (Get-Command cloudflared -ErrorAction SilentlyContinue)) {
        New-Item -ItemType Directory -Path $cfBinDir -Force | Out-Null
        Write-Host "    [*] Đang tải cloudflared.exe chính thức từ GitHub..." -ForegroundColor Yellow
        Invoke-WebRequest -Uri "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe" -OutFile $cfExe
    }

    Register-SmartStudyTask -TaskName "SmartStudy-CloudflareTunnel" -Role "CloudflareTunnel" -Description "Smart Study Workspace - Cloudflare Named Tunnel HTTPS 24/7"
}

# 4. Khởi động ngay các tác vụ nền vừa cài
Write-Host "[4/4] Khởi chạy ngay lập tức các dịch vụ nền..." -ForegroundColor Yellow
Start-ScheduledTask -TaskName "SmartStudy-AiService"
Start-Sleep -Seconds 2
Start-ScheduledTask -TaskName "SmartStudy-BackendCore"
if (-not [string]::IsNullOrWhiteSpace($CloudflareTunnelToken)) {
    Start-ScheduledTask -TaskName "SmartStudy-CloudflareTunnel"
}

Write-Host "[*] Đang chờ Backend Core sẵn sàng trên cổng 5000..." -ForegroundColor DarkGray
$ready = $false
for ($i = 0; $i -lt 15; $i++) {
    try {
        $resp = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/health" -TimeoutSec 2
        if ($resp.success) {
            $ready = $true
            break
        }
    } catch {
        Start-Sleep -Seconds 2
    }
}

Write-Host ""
Write-Host "===============================================================================" -ForegroundColor Cyan
if ($ready) {
    Write-Host "  [HOÀN TẤT] DỊCH VỤ CHẠY NGẦM ĐÃ HOẠT ĐỘNG! KHÔNG CẦN MỞ RUN.BAT NỮA!" -ForegroundColor Green
} else {
    Write-Host "  [ĐÃ CÀI ĐẶT] Dịch vụ đang khởi động ngầm. Chạy service-status.bat để kiểm tra." -ForegroundColor Yellow
}
Write-Host "  * Địa chỉ máy tính:       http://localhost:5000"
Write-Host "  * Kiểm tra phiên bản OTA: http://localhost:5000/api/system/version"
Write-Host "  * Xem trạng thái dịch vụ: scripts\background-service\service-status.bat"
Write-Host "  * Gỡ bỏ dịch vụ ngầm:     scripts\background-service\uninstall-windows-service.ps1"
Write-Host "===============================================================================" -ForegroundColor Cyan
