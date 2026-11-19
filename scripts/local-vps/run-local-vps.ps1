<#
==============================================================================
 SMART STUDY WORKSPACE - MỞ RA INTERNET: LINK HTTPS CLOUDFLARE + QR PORTAL
==============================================================================
 Đường hầm Cloudflare HTTPS do daemon-watchdog.ps1 chạy ngầm 24/7 quản lý (tự mở lại khi Cloudflare
 thu hồi link, giữ nguyên link khi Redeploy). File này chỉ:
   1. Bật đường hầm Internet (xóa cờ tunnel.disabled) và bảo đảm daemon nền đang chạy
   2. Tải cloudflared.exe (nếu chưa có) vào scripts/local-vps/bin
   3. Chờ daemon kiểm chứng link https://xxxx.trycloudflare.com trả 200 OK
   4. Sinh trang QR Portal (mã QR tạo ngay trên trình duyệt, không gửi link cho dịch vụ QR bên ngoài)
 Đóng cửa sổ này KHÔNG ảnh hưởng gì: Backend, AI và đường hầm vẫn chạy ngầm.
 Link luôn mới nhất: bấm nút QR "Mở trên Điện thoại" trên thanh điều hướng của App (Laptop).

 Tham số:
   -Headless : Chạy không tương tác (không xóa màn hình, không chờ phím)
   -NoPortal : Không tự mở trang QR Portal trên trình duyệt
   -LanOnly  : Chỉ bảo đảm hệ thống nền chạy và in link LAN (không bật đường hầm Internet)
   -Stop     : Tắt đường hầm Internet (tạo cờ tunnel.disabled; hệ thống local vẫn chạy ngầm)
==============================================================================
#>
[CmdletBinding()]
param(
    [switch]$Headless,
    [switch]$NoPortal,
    [switch]$LanOnly,
    [switch]$Stop
)

chcp 65001 > $null
try {
    [Console]::InputEncoding  = [System.Text.Encoding]::UTF8
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
} catch {
    # Không có console (chạy ngầm) thì bỏ qua thiết lập mã hóa
}
$ErrorActionPreference = "Continue"
$ProgressPreference    = "SilentlyContinue"   # Tăng tốc Invoke-WebRequest trên Windows PowerShell 5.1
. (Join-Path $PSScriptRoot "lib\LocalVps.Common.ps1")

$PortalPath           = Join-Path $VpsRoot "qr-portal.html"
$TunnelWaitSeconds    = 150

function Write-Step([string]$Tag, [string]$Message, [string]$Color = "Yellow") {
    Write-Host ("[{0}] {1}" -f $Tag, $Message) -ForegroundColor $Color
}

function Test-DaemonRunning {
    $state = Read-JsonState $DaemonStateFile
    return [bool]($state -and (Resolve-TrackedProcess $state.daemon))
}

function Wait-ActiveTunnelUrl([int]$TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $url = Get-ActiveTunnelUrl
        if ($url) { return $url }
        if (-not $Headless) { Write-Host "." -NoNewline -ForegroundColor DarkGray }
        Start-Sleep -Seconds 3
    }
    return $null
}

# ------------------------------------------------------------------------------
# TRANG QR PORTAL (ảnh chụp link tại thời điểm tạo; link luôn mới nhất nằm ở nút QR trong App)
# ------------------------------------------------------------------------------
function Write-QrPortal([string]$PublicUrl, [string]$LanUrl, [bool]$IsTunnelOnline) {
    $encode      = { param($text) [System.Net.WebUtility]::HtmlEncode($text) }
    $publicSafe  = & $encode $PublicUrl
    $lanSafe     = & $encode $LanUrl
    $generatedAt = (Get-Date).ToString("HH:mm:ss dd/MM/yyyy")
    if ($IsTunnelOnline) {
        $statusBadge = '<span class="inline-flex items-center gap-2 px-3 py-1 rounded-full text-xs font-semibold bg-emerald-500/15 text-emerald-400 border border-emerald-500/30 mb-2"><span class="w-2 h-2 rounded-full bg-emerald-400 animate-ping"></span> HTTPS READY &bull; CÀI APP 1 CHẠM</span>'
        $publicLabel = "Đường dẫn HTTPS Toàn Cầu (Cloudflare Tunnel)"
    } else {
        $statusBadge = '<span class="inline-flex items-center gap-2 px-3 py-1 rounded-full text-xs font-semibold bg-amber-500/15 text-amber-300 border border-amber-500/30 mb-2"><span class="w-2 h-2 rounded-full bg-amber-300"></span> CHẾ ĐỘ MẠNG LAN (CHƯA CÓ HTTPS)</span>'
        $publicLabel = "Đường dẫn Mạng LAN (chưa có Cloudflare Tunnel)"
    }

    $portalHtml = @"
<!DOCTYPE html>
<html lang="vi">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Smart Study Workspace - QR Portal</title>
    <script src="https://cdn.tailwindcss.com"></script>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/qrcode-generator/1.4.4/qrcode.min.js"></script>
</head>
<body class="min-h-screen bg-slate-950 text-slate-100 flex items-center justify-center p-6" style="font-family: Inter, system-ui, sans-serif">
    <div class="max-w-4xl w-full bg-slate-900/90 border border-indigo-500/30 rounded-3xl p-8 shadow-2xl">
        <div class="flex flex-wrap items-center justify-between gap-4 border-b border-slate-800 pb-6 mb-8">
            <div>
                $statusBadge
                <h1 class="text-2xl md:text-3xl font-extrabold bg-gradient-to-r from-indigo-400 via-violet-400 to-cyan-400 bg-clip-text text-transparent">
                    Smart Study Workspace &mdash; Mở trên Điện thoại
                </h1>
                <p class="mt-1 text-xs text-slate-500">Tạo lúc $generatedAt &bull; Link luôn mới nhất: nút QR trên thanh điều hướng của App</p>
            </div>
            <a href="$publicSafe" target="_blank" rel="noopener" class="px-5 py-3 rounded-xl bg-indigo-600 hover:bg-indigo-500 font-bold text-sm text-white shadow-lg shadow-indigo-600/30 transition">
                Mở Web App Ngay &rarr;
            </a>
        </div>

        <div class="grid md:grid-cols-2 gap-8 items-center">
            <div class="bg-white p-6 rounded-2xl flex flex-col items-center justify-center text-slate-900 shadow-xl">
                <div id="qr-code" data-url="$publicSafe" class="w-64 h-64 [&>svg]:w-full [&>svg]:h-full"></div>
                <p class="mt-3 text-xs font-bold uppercase tracking-wider text-indigo-600">Quét bằng Camera iPhone hoặc Android</p>
            </div>

            <div class="space-y-5">
                <div class="bg-slate-800/80 border border-slate-700 rounded-2xl p-4">
                    <div class="text-xs font-semibold text-slate-400 uppercase mb-1">$publicLabel</div>
                    <div class="flex items-center justify-between gap-2">
                        <code class="text-indigo-300 font-bold text-sm break-all">$publicSafe</code>
                        <button type="button" data-copy="$publicSafe" class="px-3 py-1.5 rounded-lg bg-indigo-500/20 hover:bg-indigo-500/30 text-indigo-300 text-xs font-semibold shrink-0">Copy</button>
                    </div>
                </div>

                <div class="bg-slate-800/50 border border-slate-800 rounded-2xl p-4">
                    <div class="text-xs font-semibold text-slate-400 uppercase mb-1">Đường dẫn Mạng Wi-Fi Nội Bộ (LAN)</div>
                    <div class="flex items-center justify-between gap-2">
                        <code class="text-emerald-400 font-semibold text-sm break-all">$lanSafe</code>
                        <button type="button" data-copy="$lanSafe" class="px-3 py-1.5 rounded-lg bg-emerald-500/15 hover:bg-emerald-500/25 text-emerald-300 text-xs font-semibold shrink-0">Copy</button>
                    </div>
                </div>

                <div class="bg-indigo-950/40 border border-indigo-500/25 rounded-2xl p-4 space-y-2 text-sm">
                    <div class="font-bold text-indigo-300">Cài App (PWA) lên điện thoại:</div>
                    <p class="text-slate-300 text-xs leading-relaxed"><strong class="text-white">&bull; Android (Chrome):</strong> Quét mã &rarr; bấm nút <b>Tải App</b> trên thanh điều hướng.</p>
                    <p class="text-slate-300 text-xs leading-relaxed"><strong class="text-white">&bull; iPhone (Safari):</strong> Quét mã &rarr; bấm <b>Tải App</b> (hoặc <b>Chia sẻ</b> &rarr; <b>Thêm vào MH chính</b>).</p>
                    <p class="text-slate-400 text-[11px] leading-relaxed">Cài App 1 chạm, toàn màn hình và dùng Micro cần đường dẫn <b>HTTPS</b>. Quick Tunnel đổi link khi Cloudflare thu hồi; daemon tự mở link mới.</p>
                </div>
            </div>
        </div>
    </div>
    <script>
        (function () {
            var holder = document.getElementById('qr-code');
            if (window.qrcode && holder) {
                var qr = window.qrcode(0, 'M');
                qr.addData(holder.getAttribute('data-url'));
                qr.make();
                holder.innerHTML = qr.createSvgTag(6, 2);
            }
            document.querySelectorAll('[data-copy]').forEach(function (btn) {
                btn.addEventListener('click', function () {
                    navigator.clipboard.writeText(btn.getAttribute('data-copy')).then(function () {
                        btn.textContent = 'Đã chép!';
                    });
                });
            });
        })();
    </script>
</body>
</html>
"@
    Write-Utf8File $PortalPath $portalHtml
}

# ==============================================================================
# CHƯƠNG TRÌNH CHÍNH
# ==============================================================================
if ($Stop) {
    Write-Utf8File $TunnelDisabledFlag ("Tat duong ham Internet luc " + (Get-Date).ToString("o") + [Environment]::NewLine)
    if (-not (Test-DaemonRunning)) {
        Stop-TunnelLayer
        Remove-Item -Path $TunnelUrlFile -Force -ErrorAction SilentlyContinue
    }
    Write-Step "OK" "Da tat duong ham Internet (daemon dong cloudflared trong toi da 15 giay). Laptop/LAN van chay: http://localhost:5500" "Green"
    Write-Step "INFO" "Bat lai link HTTPS bang run-local-vps.bat" "Cyan"
    exit 0
}

if (-not $Headless) {
    Clear-Host
    Write-Host "======================================================================" -ForegroundColor Cyan
    Write-Host "   SMART STUDY WORKSPACE - MO RA INTERNET QUA CLOUDFLARE HTTPS TUNNEL  " -ForegroundColor Yellow
    Write-Host "======================================================================" -ForegroundColor Cyan
}

# BƯỚC 1: Bật đường hầm Internet + chuẩn bị cloudflared.exe
if ($LanOnly) {
    Write-Step "1/4" "Che do -LanOnly: khong thay doi trang thai duong ham Internet." "Cyan"
} else {
    Remove-Item -Path $TunnelDisabledFlag -Force -ErrorAction SilentlyContinue
    if (Install-Cloudflared { param($message) Write-Step "1/4" $message }) {
        Write-Step "1/4" "Cloudflared da san sang, duong ham Internet duoc BAT." "Green"
    } else {
        Write-Step "CANH BAO" "Chua co cloudflared.exe -> daemon se tu tai lai sau 10 phut. Tam dung link LAN." "Red"
    }
}

# BƯỚC 2: Bảo đảm daemon nền (Backend + AI + Tunnel) đang chạy
if ((Test-DaemonRunning) -and (Test-HttpOk $BackendHealthUrl 4)) {
    Write-Step "2/4" "He thong nen (Backend 5000/5500 + AI 8000 + Tunnel) dang chay ngam." "Green"
} else {
    Write-Step "2/4" "Dang bat he thong nen an hoan toan qua start-silent-background.vbs"
    Start-SilentDaemon
    if (-not $Headless) { Write-Host "      -> Dang cho Backend san sang" -NoNewline -ForegroundColor Gray }
    $ready = Wait-BackendHealthy -TimeoutSeconds 240 -ShowProgress:(-not $Headless)
    if (-not $Headless) { Write-Host "" }
    if (-not $ready) {
        Write-Step "LOI" "Backend chua san sang sau 240 giay. Nhat ky gan nhat:" "Red"
        Write-Host (Get-LogTail (Join-Path $LogDir "daemon.log") 10) -ForegroundColor DarkGray
        Write-Host (Get-LogTail (Join-Path $LogDir "backend.log") 15) -ForegroundColor DarkRed
        exit 1
    }
    Write-Step "2/4" "He thong nen da san sang." "Green"
}

$lanUrl = "http://$(Get-LanIpAddress):5500"

# BƯỚC 3: Chờ daemon kiểm chứng link HTTPS
$tunnelUrl = $null
if (-not $LanOnly) {
    Write-Step "3/4" "Dang cho daemon cap phat va kiem chung link HTTPS Cloudflare"
    $tunnelUrl = Wait-ActiveTunnelUrl $TunnelWaitSeconds
    if (-not $Headless) { Write-Host "" }
    if ($tunnelUrl) {
        Write-Step "3/4" "Link HTTPS da hoat dong: $tunnelUrl" "Green"
    } else {
        Write-Step "CHU Y" "Chua co link HTTPS sau $TunnelWaitSeconds giay (xem logs\tunnel.log). Tam dung link LAN." "DarkYellow"
    }
}

# BƯỚC 4: QR Portal + tóm tắt
$publicUrl = if ($tunnelUrl) { $tunnelUrl } else { $lanUrl }
Write-QrPortal -PublicUrl $publicUrl -LanUrl $lanUrl -IsTunnelOnline ([bool]$tunnelUrl)
Write-Step "4/4" "Da tao trang QR Portal: $PortalPath" "Green"
if (-not $NoPortal -and -not $LanOnly) {
    Start-Process $PortalPath
}

Write-Host ""
Write-Host "======================================================================" -ForegroundColor Green
if ($tunnelUrl) {
    Write-Host "  [1] LINK HTTPS (DIEN THOAI 4G/WIFI, CAI APP 1 CHAM): $tunnelUrl" -ForegroundColor White -BackgroundColor DarkBlue
} else {
    Write-Host "  [1] CHUA CO LINK HTTPS - dung link LAN ben duoi" -ForegroundColor Yellow
}
Write-Host "  [2] LINK MANG LAN (CUNG WIFI): $lanUrl" -ForegroundColor Green
Write-Host "  [3] LAPTOP: http://localhost:5500" -ForegroundColor Green
Write-Host "======================================================================" -ForegroundColor Green
Write-Host "  Moi thu chay ngam 24/7: dong cua so nay KHONG lam web hay link HTTPS ngung." -ForegroundColor DarkGray
Write-Host "  Tat link Internet: stop-local-vps.bat  |  Tat toan bo: stop-background.bat" -ForegroundColor DarkGray
if (-not $Headless) {
    Write-Host ""
    Write-Host "Nhan phim bat ky de dong cua so..." -ForegroundColor DarkGray
    try { [void][Console]::ReadKey($true) } catch { }
}
exit 0
