<#
===============================================================================
 Smart Study Workspace — Kiểm tra trạng thái Windows Background Services
===============================================================================
#>
$ErrorActionPreference = "Continue"

Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host "   SMART STUDY WORKSPACE - KIEM TRA TRANG THAI DICH VU CHAY NGAM (24/7)        " -ForegroundColor Cyan
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host ""

$tasks = @("SmartStudy-AiService", "SmartStudy-BackendCore", "SmartStudy-CloudflareTunnel")
Write-Host "[1] WINDOWS SCHEDULED TASKS:" -ForegroundColor Cyan
foreach ($t in $tasks) {
    $obj = Get-ScheduledTask -TaskName $t -ErrorAction SilentlyContinue
    if ($obj) {
        $color = if ($obj.State -eq "Running") { "Green" } else { "Yellow" }
        Write-Host ("    - {0,-30}: {1}" -f $t, $obj.State) -ForegroundColor $color
    } else {
        Write-Host ("    - {0,-30}: Chua cai dat" -f $t) -ForegroundColor DarkGray
    }
}

Write-Host ""
Write-Host "[2] KIEM TRA KET NOI DICH VU THUC TE:" -ForegroundColor Cyan
try {
    $ai = Invoke-RestMethod -Uri "http://127.0.0.1:8000/api/v1/health" -TimeoutSec 3
    $aiStatus = if ($ai -and $ai.status) { $ai.status } else { "OK" }
    Write-Host ("    - AI Microservice (8000)      : ONLINE ({0})" -f $aiStatus) -ForegroundColor Green
} catch {
    Write-Host "    - AI Microservice (8000)      : OFFLINE" -ForegroundColor Red
}

try {
    $be = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/health" -TimeoutSec 3
    $beStatus = if ($be -and $be.data -and $be.data.status) { $be.data.status } else { "Healthy" }
    Write-Host ("    - Backend All-in-One (5000)   : ONLINE ({0})" -f $beStatus) -ForegroundColor Green
} catch {
    Write-Host "    - Backend All-in-One (5000)   : OFFLINE" -ForegroundColor Red
}

try {
    $ver = Invoke-RestMethod -Uri "http://127.0.0.1:5000/api/system/version" -TimeoutSec 3
    Write-Host ("    - OTA Build Version           : {0}" -f $ver.data.version) -ForegroundColor Magenta
} catch {
    Write-Host "    - OTA Build Version           : Khong the doc" -ForegroundColor DarkYellow
}

$sqlSvc = Get-Service -Name 'MSSQL$SQLEXPRESS', 'MSSQLSERVER' -ErrorAction SilentlyContinue | Select-Object -First 1
if ($sqlSvc) {
    $sc = if ($sqlSvc.Status -eq "Running") { "Green" } else { "Red" }
    Write-Host ("    - SQL Server ({0,-14}) : {1}" -f $sqlSvc.Name, $sqlSvc.Status) -ForegroundColor $sc
} else {
    Write-Host "    - SQL Server Local Service    : Khong tim thay (co the dang dung Docker)" -ForegroundColor Yellow
}

Write-Host ""
$lanIp = (Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.InterfaceAlias -match "Wi-Fi|Ethernet" -and $_.IPAddress -notmatch "^169\." } |
    Select-Object -First 1).IPAddress

Write-Host "[3] DIA CHI TRUY CAP:" -ForegroundColor Cyan
Write-Host "    - May tinh (PC/Laptop)        : http://localhost:5000" -ForegroundColor White
if ($lanIp) {
    Write-Host ("    - Dien thoai cung Wi-Fi       : http://{0}:5000" -f $lanIp) -ForegroundColor White
}
$tokenFile = Join-Path $PSScriptRoot ".cloudflare-token"
if (Test-Path $tokenFile) {
    Write-Host "    - Cloudflare Named Tunnel     : Da cau hinh (truy cap qua ten mien HTTPS co dinh cua ban)" -ForegroundColor Green
}
Write-Host ""
Write-Host "===============================================================================" -ForegroundColor Cyan
