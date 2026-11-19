@echo off
REM ===============================================================================
REM   SMART STUDY WORKSPACE - BAT TU CHAY NGAM CUNG WINDOWS (KHONG CAN QUYEN ADMIN)
REM   Tao file SmartStudyAutoStart.vbs trong thu muc Startup cua nguoi dung hien tai.
REM   Moi lan dang nhap Windows: Backend (5000 + 5500) va AI Service (8000) tu chay ngam,
REM   khong hien cua so nao. Muon mo ra Internet (HTTPS) thi chay them run-local-vps.bat.
REM ===============================================================================
chcp 65001 >nul
title Bat tu chay Smart Study cung Windows
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0daemon-watchdog.ps1" -InstallAutostart
pause
