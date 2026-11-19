<#
==============================================================================
 SMART STUDY WORKSPACE - MỞ TƯỜNG LỬA CHO ĐIỆN THOẠI CÙNG WI-FI (CHẠY 1 LẦN DUY NHẤT)
 Windows Firewall gắn luật theo đường dẫn file chạy, nên bản Backend chạy ngầm
 (publish-backend\SmartStudy.API.exe) cần 1 luật Inbound riêng để điện thoại truy cập http://<IP>:5500.
 - Chỉ cho phép cổng 5000/5500 và CHỈ thiết bị trong cùng mạng con (RemoteAddress = LocalSubnet).
 - Gỡ luật "Block" mà Windows có thể tự tạo khi hộp thoại tường lửa bị bấm Hủy.
 - Tự yêu cầu UAC (quyền Admin) đúng 1 lần khi bạn chủ động chạy file này; không liên quan tới tự khởi động.
==============================================================================
#>
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Start-Process -FilePath "powershell.exe" -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    exit 0
}

. (Join-Path $PSScriptRoot "lib\LocalVps.Common.ps1")
$ruleName = "SmartStudy Backend LAN (TCP 5000, 5500)"

if (-not (Test-Path $BackendExe)) {
    Write-Host "[LOI] Chua co ban publish: $BackendExe. Hay chay start-app.bat truoc." -ForegroundColor Red
    Read-Host "Nhan Enter de dong"
    exit 1
}

# Gỡ luật cũ cùng tên và mọi luật Block gắn với chương trình này
Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue | Remove-NetFirewallRule
Get-NetFirewallApplicationFilter -ErrorAction SilentlyContinue |
    Where-Object { $_.Program -ieq $BackendExe } |
    Get-NetFirewallRule |
    Where-Object { $_.Action -eq "Block" } |
    Remove-NetFirewallRule

New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Program $BackendExe -Protocol TCP -LocalPort 5000, 5500 `
    -Action Allow -Profile Private, Public -RemoteAddress LocalSubnet | Out-Null

Write-Host "[OK] Da mo tuong lua cho dien thoai cung mang Wi-Fi: http://$(Get-LanIpAddress):5500" -ForegroundColor Green
Read-Host "Nhan Enter de dong"
