<#
===============================================================================
 Smart Study Workspace — Gỡ bỏ Windows Background Service sạch sẽ
 Dừng và xóa toàn bộ Windows Scheduled Tasks (SmartStudy-*) cùng các tiến trình con.
===============================================================================
#>
[CmdletBinding()]
param()

$ErrorActionPreference = "Continue"
$BgDir = $PSScriptRoot

Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host "   SMART STUDY WORKSPACE — GỠ BỎ DỊCH VỤ CHẠY NGẦM WINDOWS                    " -ForegroundColor Cyan
Write-Host "===============================================================================" -ForegroundColor Cyan

$taskNames = @(
    "SmartStudy-AiService",
    "SmartStudy-BackendCore",
    "SmartStudy-CloudflareTunnel"
)

foreach ($name in $taskNames) {
    $existing = Get-ScheduledTask -TaskName $name -ErrorAction SilentlyContinue
    if ($existing) {
        Write-Host "[*] Đang dừng và gỡ bỏ Scheduled Task: $name" -ForegroundColor Yellow
        Stop-ScheduledTask -TaskName $name -ErrorAction SilentlyContinue
        Unregister-ScheduledTask -TaskName $name -Confirm:$false -ErrorAction SilentlyContinue
        Write-Host "    [OK] Đã xóa $name." -ForegroundColor Green
    } else {
        Write-Host "    [INFO] Không tìm thấy task $name." -ForegroundColor DarkGray
    }
}

# Dừng các tiến trình supervisor PowerShell & các dịch vụ đang chiếm cổng 5000, 8000
Write-Host "[*] Dọn dẹp các tiến trình nền còn sót lại..." -ForegroundColor Yellow
Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -like "*run-hidden-worker.ps1*" } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

$ports = @(5000, 8000)
$pids = Get-NetTCPConnection -LocalPort $ports -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique
foreach ($p in $pids) {
    if ($p -gt 4) {
        Stop-Process -Id $p -Force -ErrorAction SilentlyContinue
    }
}

# Xóa file token lưu tạm nếu có
$tokenFile = Join-Path $BgDir ".cloudflare-token"
if (Test-Path $tokenFile) {
    Remove-Item -Path $tokenFile -Force -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host "   [HOÀN TẤT] ĐÃ GỠ BỎ TOÀN BỘ DỊCH VỤ CHẠY NGẦM THÀNH CÔNG!" -ForegroundColor Green
Write-Host "===============================================================================" -ForegroundColor Cyan
