@echo off
REM ===============================================================================
REM   SMART STUDY WORKSPACE - TAT HE THONG CHAY NGAM
REM   Tat sach Watchdog, Backend (5000 + 5500), AI Service (8000) va Cloudflare Tunnel
REM   de giai phong RAM. Bat lai: start-app.bat hoac start-silent-background.vbs
REM ===============================================================================
chcp 65001 >nul
title Stop Smart Study Background
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0daemon-watchdog.ps1" -Stop
timeout /t 3
