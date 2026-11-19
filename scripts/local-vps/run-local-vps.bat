@echo off
REM ===============================================================================
REM   SMART STUDY WORKSPACE - LOCAL VPS 24/7 (Backend + Frontend + AI + Cloudflare Tunnel)
REM   Tham so tuy chon chuyen tiep cho PowerShell: -LanOnly  -NoPortal  -Headless
REM ===============================================================================
chcp 65001 >nul
title Smart Study Workspace - Local VPS 24/7
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-local-vps.ps1" %*
if errorlevel 1 pause
