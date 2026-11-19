@echo off
REM ===============================================================================
REM   SMART STUDY WORKSPACE - DONG DUONG HAM INTERNET (CLOUDFLARE TUNNEL)
REM   Chi dong link HTTPS cong khai. He thong local (Backend 5000 + 5500, AI 8000)
REM   van chay ngam cho laptop va dien thoai cung Wi-Fi.
REM   Muon tat han toan bo: chay stop-background.bat
REM ===============================================================================
chcp 65001 >nul
title Dong Cloudflare Tunnel - Smart Study
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-local-vps.ps1" -Stop
timeout /t 3
