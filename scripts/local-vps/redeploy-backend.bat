@echo off
REM ===============================================================================
REM   SMART STUDY WORKSPACE - CAP NHAT BAN BACKEND CHAY NGAM SAU KHI SUA CODE C#
REM   Tat Backend + AI -> dotnet publish -c Release -> bat lai chay ngam.
REM   Cac thiet bi dang mo App se tu dong tai giao dien moi nho OTA Auto-Sync.
REM   (Sua code Frontend HTML/JS/CSS thi KHONG can chay file nay - ap dung ngay lap tuc.)
REM ===============================================================================
chcp 65001 >nul
title Smart Study - Redeploy Backend
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0daemon-watchdog.ps1" -Redeploy
pause
