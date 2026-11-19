@echo off
REM Smart Study Workspace -- Kiem tra nhanh Trang thai Dich vu Chay ngam 24/7
chcp 65001 >nul
title Trang thai Dich vu Nen - Smart Study Workspace
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0check-status.ps1"
pause
