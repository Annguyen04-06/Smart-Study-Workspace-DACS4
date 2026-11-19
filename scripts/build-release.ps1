# =============================================================================
# Smart Study Workspace — Automated 1-Click Release Package Generator
# =============================================================================
[CmdletBinding()]
param (
    [string]$OutputDir = "release-package"
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir = Split-Path -Parent $ScriptDir
$TargetDir = Join-Path $RootDir $OutputDir

Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host "      SMART STUDY WORKSPACE — TIẾN HÀNH ĐÓNG GÓI BẢN PHÁT HÀNH SẢN XUẤT       " -ForegroundColor Cyan
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host "[*] Thư mục gốc dự án:   $RootDir"
Write-Host "[*] Thư mục xuất bản:     $TargetDir"
Write-Host ""

# 1. Dọn dẹp thư mục release cũ nếu tồn tại
if (Test-Path $TargetDir) {
    Write-Host "[*] Đang xóa sạch thư mục phát hành cũ..." -ForegroundColor Yellow
    Remove-Item -Path $TargetDir -Recurse -Force
}
New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null

# 2. Biên dịch và xuất bản Backend Core (.NET 8 Clean Architecture)
Write-Host "[1/5] Đang biên dịch và xuất bản Backend Core (Release mode)..." -ForegroundColor Green
$BackendProj = Join-Path $RootDir "backend-core\src\SmartStudy.API\SmartStudy.API.csproj"
$BackendOut = Join-Path $TargetDir "backend"

dotnet publish $BackendProj -c Release -o $BackendOut /p:UseAppHost=false
if ($LASTEXITCODE -ne 0) {
    Write-Error "Xuất bản Backend thất bại với mã lỗi $LASTEXITCODE"
}

# Tạo thư mục uploads trong backend
New-Item -ItemType Directory -Path (Join-Path $BackendOut "wwwroot\uploads\materials") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $BackendOut "wwwroot\uploads\avatars") -Force | Out-Null

# Loại bỏ file cấu hình bí mật cục bộ nếu bị copy theo publish
Remove-Item -Path (Join-Path $BackendOut "appsettings.Local.json") -Force -ErrorAction SilentlyContinue

# Sao chép các file PDF mẫu đã sinh sẵn sang thư mục release wwwroot
$SourceMaterials = Join-Path $RootDir "backend-core\src\SmartStudy.API\wwwroot\uploads\materials"
if (Test-Path $SourceMaterials) {
    Copy-Item -Path "$SourceMaterials\*" -Destination (Join-Path $BackendOut "wwwroot\uploads\materials") -Recurse -Force -ErrorAction SilentlyContinue
}

# 3. Sao chép Tầng Frontend PWA
Write-Host "[2/5] Đang sao chép Frontend PWA (HTML5, Tailwind, ES6 Modules)..." -ForegroundColor Green
$FrontendSource = Join-Path $RootDir "frontend"
$FrontendDest = Join-Path $TargetDir "frontend"
New-Item -ItemType Directory -Path $FrontendDest -Force | Out-Null

Get-ChildItem -Path $FrontendSource -Exclude ".git", ".vscode" | ForEach-Object {
    Copy-Item -Path $_.FullName -Destination $FrontendDest -Recurse -Force
}

# 4. Sao chép Tầng AI Microservice (Loại bỏ .venv và cache)
Write-Host "[3/5] Đang sao chép AI Microservice (FastAPI, Google GenAI SDK)..." -ForegroundColor Green
$AiSource = Join-Path $RootDir "ai-service"
$AiDest = Join-Path $TargetDir "ai-service"
New-Item -ItemType Directory -Path $AiDest -Force | Out-Null

Get-ChildItem -Path $AiSource -Exclude ".venv", "venv", "__pycache__", ".env", "*.pyc" | ForEach-Object {
    Copy-Item -Path $_.FullName -Destination $AiDest -Recurse -Force
}

# 5. Sao chép Cơ sở dữ liệu SQL Server & Docker Compose
Write-Host "[4/5] Đang sao chép Database scripts và Docker artifacts..." -ForegroundColor Green
$DbSource = Join-Path $RootDir "database"
$DbDest = Join-Path $TargetDir "database"
Copy-Item -Path $DbSource -Destination $DbDest -Recurse -Force

Copy-Item -Path (Join-Path $RootDir "docker-compose.yml") -Destination $TargetDir -Force
Copy-Item -Path (Join-Path $RootDir ".env.example") -Destination (Join-Path $TargetDir ".env.example") -Force

# 6. Tạo file khởi động nhanh trong gói phát hành (start-release.bat & stop-release.bat)
Write-Host "[5/5] Đang tạo script điều khiển cho gói phát hành độc lập..." -ForegroundColor Green

$StartReleaseBat = @"
@echo off
REM ===============================================================================
REM     SMART STUDY WORKSPACE -- RELEASE 1-CLICK LAUNCHER
REM ===============================================================================
chcp 65001 >nul
title Smart Study Workspace (Release)

set "RELEASE_DIR=%~dp0"

echo ===============================================================================
echo     SMART STUDY WORKSPACE - KHOI DONG BAN PHAT HANH SAN XUAT (RELEASE)
echo ===============================================================================
echo [*] Thu muc: %RELEASE_DIR%
echo.

echo [*] Don dep cac cong 5000, 8000, 5500 neu dang ban...
powershell -NoProfile -ExecutionPolicy Bypass -Command "`$ports = 5000, 8000, 5500; `$pids = Get-NetTCPConnection -LocalPort `$ports -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique; if (`$pids) { foreach (`$p in `$pids) { if (`$p -gt 4) { Stop-Process -Id `$p -Force -ErrorAction SilentlyContinue } } }"

echo [*] [1/3] Dang khoi chay AI Microservice (Port 8000)...
start "Smart Study - AI Microservice" /D "%RELEASE_DIR%ai-service" cmd /k "python -m uvicorn app.main:app --host 127.0.0.1 --port 8000"

echo [*] [2/3] Dang khoi chay Backend Core .NET 8 (Port 5000)...
start "Smart Study - Backend Core" /D "%RELEASE_DIR%backend" cmd /k "dotnet SmartStudy.API.dll --urls http://localhost:5000"

echo [*] [3/3] Dang khoi chay Frontend PWA Server (Port 5500)...
start "Smart Study - Frontend PWA" /D "%RELEASE_DIR%frontend" cmd /k "python -m http.server 5500"

echo.
echo [*] Dang cho he thong san sang (khoang 5 giay)...
powershell -NoProfile -Command "Start-Sleep -Seconds 5"

echo.
echo ===============================================================================
echo     HE THONG DA SAN SANG TAI: http://localhost:5500
echo ===============================================================================
start http://localhost:5500
pause >nul
"@

Set-Content -Path (Join-Path $TargetDir "start-release.bat") -Value $StartReleaseBat -Encoding UTF8
Copy-Item -Path (Join-Path $RootDir "scripts\stop-app.bat") -Destination (Join-Path $TargetDir "stop-release.bat") -Force

# Tạo file README hướng dẫn bên trong release
$ReadmeRelease = @"
===============================================================================
       HƯỚNG DẪN VẬN HÀNH BẢN PHÁT HÀNH — SMART STUDY WORKSPACE (VKU)
===============================================================================

1. YÊU CẦU MÔI TRƯỜNG:
   - Windows 10 / 11 64-bit
   - .NET 8 ASP.NET Core Runtime (hoặc .NET 8 SDK)
   - Python 3.11+ (đã cài đặt các thư viện trong ai-service/requirements.txt)
   - SQL Server 2019/2022 hoặc LocalDB / Docker SQL Server

2. CÁCH KHỞI CHẠY BẰNG 1-CLICK:
   - Nhấp đúp chuột vào file: start-release.bat
   - Trình duyệt sẽ tự động mở trang chủ tại địa chỉ: http://localhost:5500

3. CÁCH DỪNG TOÀN BỘ ỨNG DỤNG:
   - Nhấp đúp chuột vào file: stop-release.bat

4. CÁCH TRIỂN KHAI BẰNG DOCKER COMPOSE:
   - Mở terminal tại thư mục này và gõ:
     docker compose up -d --build
   - Truy cập ngay tại: http://localhost (Cổng 80)

5. TÀI KHOẢN MẪU ĐĂNG NHẬP TRẢI NGHIỆM:
   - Sinh viên VKU: an.nv.23it@vku.udn.vn  (Mật khẩu: Student@123)
   - Quản trị viên: admin@vku.udn.vn       (Mật khẩu: Admin@123)
"@

Set-Content -Path (Join-Path $TargetDir "README_RELEASE.txt") -Value $ReadmeRelease -Encoding UTF8

# Tính toán dung lượng gói phát hành
$TotalBytes = (Get-ChildItem -Path $TargetDir -Recurse | Measure-Object -Property Length -Sum).Sum
$TotalMB = [math]::Round($TotalBytes / 1MB, 2)
$TotalFiles = (Get-ChildItem -Path $TargetDir -Recurse -File).Count

Write-Host ""
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host "       [HOÀN TẤT] ĐÃ ĐÓNG GÓI BẢN PHÁT HÀNH RELEASE PACKAGE THÀNH CÔNG!        " -ForegroundColor Green
Write-Host "===============================================================================" -ForegroundColor Cyan
Write-Host "  * Thư mục đích:      $TargetDir"
Write-Host "  * Tổng số file:      $TotalFiles tệp tin"
Write-Host "  * Tổng dung lượng:   $TotalMB MB"
Write-Host "  * Script khởi chạy:  $TargetDir\start-release.bat"
Write-Host "===============================================================================" -ForegroundColor Cyan
