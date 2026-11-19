@echo off
REM ===============================================================================
REM     SMART STUDY WORKSPACE -- DUNG HE THONG
REM   Tat sach Watchdog chay ngam, Backend (5000 + 5500), AI Service (8000)
REM   va Cloudflare Tunnel (neu dang mo). Bat lai bang start-app.bat.
REM ===============================================================================
chcp 65001 >nul
title Dung He thong Smart Study Workspace
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0local-vps\daemon-watchdog.ps1" -Stop
echo.
pause
