<#
===============================================================================
 Smart Study Workspace — Hidden Background Worker Supervisor
 Được gọi ẩn hoàn toàn bởi Windows Scheduled Task (không hiện cửa sổ console).
 Hỗ trợ tự khởi động lại tiến trình con nếu bị lỗi đột ngột (Watchdog Loop).
===============================================================================
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("AiService", "BackendCore", "CloudflareTunnel")]
    [string]$Role,

    [Parameter(Mandatory = $true)]
    [string]$ProjectRoot,

    [string]$TunnelToken = ""
)

$ErrorActionPreference = "Continue"
$BgDir  = $PSScriptRoot
$LogDir = Join-Path $BgDir "logs"
New-Item -ItemType Directory -Path $LogDir -Force | Out-Null

function Write-WorkerLog([string]$Message) {
    $line = "{0} | [{1}] {2}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $Role, $Message
    Add-Content -Path (Join-Path $LogDir "$Role-supervisor.log") -Value $line -Encoding UTF8
}

$OutLog = Join-Path $LogDir "$Role-stdout.log"
$ErrLog = Join-Path $LogDir "$Role-stderr.log"

Write-WorkerLog "Khởi động supervisor nền tại ProjectRoot=$ProjectRoot"

while ($true) {
    try {
        $filePath = ""
        $arguments = ""
        $workingDir = $ProjectRoot

        if ($Role -eq "AiService") {
            $workingDir = Join-Path $ProjectRoot "ai-service"
            $venvPython = Join-Path $workingDir ".venv\Scripts\python.exe"
            $filePath = if (Test-Path $venvPython) { $venvPython } else { "python" }
            $arguments = "-m uvicorn app.main:app --host 127.0.0.1 --port 8000"
        }
        elseif ($Role -eq "BackendCore") {
            $publishedDir = Join-Path $BgDir "published-backend"
            $publishedDll = Join-Path $publishedDir "SmartStudy.API.dll"
            $frontendDir  = Join-Path $ProjectRoot "frontend"
            $env:FrontendHosting__RootPath = $frontendDir
            $env:ASPNETCORE_URLS = "http://0.0.0.0:5000"

            if (Test-Path $publishedDll) {
                $workingDir = $publishedDir
                $filePath = "dotnet"
                $arguments = "`"$publishedDll`" --urls http://0.0.0.0:5000"
            } else {
                $workingDir = Join-Path $ProjectRoot "backend-core\src\SmartStudy.API"
                $filePath = "dotnet"
                $arguments = "run --no-launch-profile --urls http://0.0.0.0:5000"
            }
        }
        elseif ($Role -eq "CloudflareTunnel") {
            $tokenToUse = $TunnelToken
            if ([string]::IsNullOrWhiteSpace($tokenToUse)) {
                $tokenFile = Join-Path $BgDir ".cloudflare-token"
                if (Test-Path $tokenFile) {
                    $tokenToUse = (Get-Content -Path $tokenFile -Raw).Trim()
                }
            }
            if ([string]::IsNullOrWhiteSpace($tokenToUse)) {
                Write-WorkerLog "Không có CLOUDFLARE_TUNNEL_TOKEN. Thoát worker CloudflareTunnel."
                break
            }

            $cfExe = Join-Path $ProjectRoot "scripts\local-vps\bin\cloudflared.exe"
            if (-not (Test-Path $cfExe)) {
                $cfExe = "cloudflared"
            }
            $filePath = $cfExe
            $arguments = "tunnel --no-autoupdate run --token $tokenToUse"
        }

        Write-WorkerLog "Thực thi: $filePath $arguments (WorkDir: $workingDir)"
        $proc = Start-Process -FilePath $filePath `
            -ArgumentList $arguments `
            -WorkingDirectory $workingDir `
            -WindowStyle Hidden `
            -RedirectStandardOutput $OutLog `
            -RedirectStandardError $ErrLog `
            -PassThru

        $proc.WaitForExit()
        Write-WorkerLog "Tiến trình dừng với ExitCode=$($proc.ExitCode). Tự khởi động lại sau 8 giây..."
    }
    catch {
        Write-WorkerLog "Lỗi supervisor: $($_.Exception.Message). Thử lại sau 10 giây..."
    }

    Start-Sleep -Seconds 8
}
