@echo off
REM ===============================================================================
REM       SMART STUDY WORKSPACE -- 1-CLICK LAUNCHER (PC & MOBILE SUPPORT)
REM   Bat he thong CHAY NGAM 24/7 (khong mo cua so den nao):
REM     - C# Backend All-in-One: giao dien + API + SignalR tren cong 5000 VA 5500
REM     - Python AI Microservice: cong 8000 (noi bo)
REM   Dong cua so nay hay tat terminal deu KHONG lam web ngung chay.
REM   Tat han: stop-app.bat | Mo ra Internet HTTPS: local-vps\run-local-vps.bat
REM   Tai khoan test: xem file TAI-KHOAN-TEST.txt o thu muc goc du an (khong in ra day).
REM ===============================================================================
chcp 65001 >nul
title Smart Study Workspace Launcher

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0local-vps\daemon-watchdog.ps1" -Start
if errorlevel 1 goto :error

REM Tu dong mo trinh duyet mac dinh tren may tinh
start "" http://localhost:5500

echo Nhan phim bat ky de dong cua so nay (he thong van tiep tuc chay ngam).
pause >nul
exit /b 0

:error
echo.
echo ===============================================================================
echo     CO LOI XAY RA TRONG QUA TRINH KHOI DONG - XEM NHAT KY TRONG scripts\local-vps\logs
echo ===============================================================================
pause
exit /b 1
