@echo off
REM ===============================================================================
REM   SMART STUDY WORKSPACE - TAT TU CHAY NGAM CUNG WINDOWS
REM   Xoa SmartStudyAutoStart.vbs (va file cu SmartStudyLocalVPS.cmd neu con) khoi thu muc Startup.
REM   Khong tat he thong dang chay - muon tat ngay hay chay stop-background.bat.
REM ===============================================================================
chcp 65001 >nul
title Tat tu chay Smart Study cung Windows
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0daemon-watchdog.ps1" -UninstallAutostart
pause
